using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;

namespace Dsw2026Tpi.Application.Services;

public class AvailabilityService : IAvailabilityService
{
    private readonly IPersistence _persistence;
    private readonly IHolidayService _holidayService;
    private readonly IClockService _clockService;

    // Orden semanal (LUNES a DOMINGO) usado tanto para ordenar la respuesta del GET
    // como para validar/mapear los nombres de día que llegan en el request.
    private static readonly string[] WeekOrder =
        ["LUNES", "MARTES", "MIÉRCOLES", "JUEVES", "VIERNES", "SÁBADO", "DOMINGO"];

    // Acepta variantes con y sin tilde en el input, pero siempre normaliza al nombre
    // canónico (con tilde) tanto para guardar en base como para responder al frontend.
    private static readonly Dictionary<string, (DayOfWeek DayOfWeek, string Canonical)> DayNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["LUNES"] = (DayOfWeek.Monday, "LUNES"),
            ["MARTES"] = (DayOfWeek.Tuesday, "MARTES"),
            ["MIERCOLES"] = (DayOfWeek.Wednesday, "MIÉRCOLES"),
            ["MIÉRCOLES"] = (DayOfWeek.Wednesday, "MIÉRCOLES"),
            ["JUEVES"] = (DayOfWeek.Thursday, "JUEVES"),
            ["VIERNES"] = (DayOfWeek.Friday, "VIERNES"),
            ["SABADO"] = (DayOfWeek.Saturday, "SÁBADO"),
            ["SÁBADO"] = (DayOfWeek.Saturday, "SÁBADO"),
            ["DOMINGO"] = (DayOfWeek.Sunday, "DOMINGO"),
        };

    public AvailabilityService(IPersistence persistence, IHolidayService holidayService, IClockService clockService)
    {
        _persistence = persistence;
        _holidayService = holidayService;
        _clockService = clockService;
    }

    public async Task<IEnumerable<AvailabilityModel.DayResponse>> GetByDoctor(Guid doctorId)
    {
        _ = await _persistence.GetById<Doctor>(doctorId)
            ?? throw new EntityNotFoundException(nameof(Doctor));

        // Decisión de diseño (1): la disponibilidad consultada es siempre la del mes/año
        // actual del servidor, no se admite consultar meses futuros o pasados por este endpoint.
        // Se usa hora local de Argentina (no UTC) para evitar desfasajes cerca de medianoche.
        var now = _clockService.Now;

        var rules = await _persistence.GetFiltered<AvailabilityRule>(
            r => r.DoctorId == doctorId && r.Month == now.Month && r.Year == now.Year);

        // Si nunca se cargó disponibilidad para el médico/mes, esto devuelve una lista vacía,
        // tal como pide el TPI para médicos nuevos o meses nuevos.
        return (rules ?? [])
            .OrderBy(r => Array.IndexOf(WeekOrder, r.DayOfWeek))
            .Select(r => new AvailabilityModel.DayResponse(r.DayOfWeek, Format(r.StartTime), Format(r.EndTime)));
    }

    public async Task Create(AvailabilityModel.Request request)
    {
        _ = await _persistence.GetById<Doctor>(request.DoctorId)
            ?? throw new EntityNotFoundException(nameof(Doctor));

        var parsedDays = ParseAndValidate(request);
        CheckInternalOverlaps(parsedDays);

        // Decisión de diseño (1): siempre se trabaja sobre el mes/año actual, en hora local Argentina.
        var now = _clockService.Now;

        var existingRules = (await _persistence.GetFiltered<AvailabilityRule>(
            r => r.DoctorId == request.DoctorId && r.Month == now.Month && r.Year == now.Year)) ?? [];

        foreach (var day in parsedDays)
        {
            var sameDayRules = existingRules.Where(r => r.DayOfWeek == day.Canonical).ToList();

            // RN: "No se permiten solapamientos de horarios para el mismo médico"
            var overlapping = sameDayRules.FirstOrDefault(r => Overlaps(r.StartTime, r.EndTime, day.Start, day.End));

            if (overlapping is not null)
            {
                // POST es aditivo/idempotente: si la regla ya existe tal cual, no la duplicamos.
                if (overlapping.StartTime == day.Start && overlapping.EndTime == day.End)
                    continue;

                throw new ConflictException(
                    "AVAILABILITY_OVERLAP",
                    $"Ya existe una disponibilidad que se solapa para el médico el día {day.Canonical}.");
            }

            var rule = new AvailabilityRule(request.DoctorId, now.Month, now.Year, day.Canonical, day.Start, day.End);
            await _persistence.Add(rule);

            await GenerateSlots(rule, day.DayOfWeek, now, protectedSlots: []);
        }
    }

    public async Task Update(AvailabilityModel.Request request)
    {
        _ = await _persistence.GetById<Doctor>(request.DoctorId)
            ?? throw new EntityNotFoundException(nameof(Doctor));

        var parsedDays = ParseAndValidate(request);
        CheckInternalOverlaps(parsedDays);

        // Decisión de diseño (1): el PUT sobrescribe únicamente el mes/año actual, en hora local Argentina.
        var now = _clockService.Now;

        var existingRules = (await _persistence.GetFiltered<AvailabilityRule>(
            r => r.DoctorId == request.DoctorId && r.Month == now.Month && r.Year == now.Year)) ?? [];

        // Decisión de diseño (2): si un slot ya tiene una cita BOOKED asociada, no se toca
        // (no se desactiva ni se regenera). El resto de los slots/reglas del mes sí se
        // sobrescriben normalmente. Guardamos esos slots protegidos para no generar un
        // slot duplicado en la misma fecha/hora al recrear las reglas más abajo.
        var protectedSlots = new HashSet<(DateOnly Date, TimeSpan Start)>();

        foreach (var rule in existingRules)
        {
            var slots = (await _persistence.GetFiltered<AvailabilitySlot>(s => s.AvailabilityRuleId == rule.Id)) ?? [];
            var hasProtectedSlot = false;

            foreach (var slot in slots)
            {
                var bookedAppointment = await _persistence.First<Appointment>(
                    a => a.AvailabilitySlotId == slot.Id && a.Status == AppointmentStatus.BOOKED);

                if (bookedAppointment is not null)
                {
                    hasProtectedSlot = true;
                    protectedSlots.Add((DateOnly.FromDateTime(slot.SlotDate), slot.StartTime));
                    continue;
                }

                await _persistence.SoftDelete(slot);
            }

            // Si ningún slot de la regla está protegido, se desactiva la regla completa.
            // Si quedó algún slot protegido, la regla se mantiene activa como referencia.
            if (!hasProtectedSlot)
                await _persistence.SoftDelete(rule);
        }

        foreach (var day in parsedDays)
        {
            var rule = new AvailabilityRule(request.DoctorId, now.Month, now.Year, day.Canonical, day.Start, day.End);
            await _persistence.Add(rule);

            await GenerateSlots(rule, day.DayOfWeek, now, protectedSlots);
        }
    }

    private async Task GenerateSlots(AvailabilityRule rule, DayOfWeek dayOfWeek, DateTime referenceDate,
        HashSet<(DateOnly Date, TimeSpan Start)> protectedSlots)
    {
        // Decisión de diseño (1): los slots se generan desde HOY (inclusive) hasta el
        // último día del mes actual, no desde el día 1 del mes ni para meses siguientes.
        var startDate = referenceDate.Date;
        var endOfMonth = new DateTime(referenceDate.Year, referenceDate.Month,
            DateTime.DaysInMonth(referenceDate.Year, referenceDate.Month));

        for (var date = startDate; date <= endOfMonth; date = date.AddDays(1))
        {
            if (date.DayOfWeek != dayOfWeek) continue;

            // Decisión de diseño (3): se excluyen los feriados configurados en appsettings.json.
            if (_holidayService.IsHoliday(date)) continue;

            for (var time = rule.StartTime; time < rule.EndTime; time = time.Add(TimeSpan.FromMinutes(30)))
            {
                // Evita crear un slot duplicado en la misma fecha/hora de un slot ya
                // protegido por una cita BOOKED (ver decisión de diseño (2) en Update).
                if (protectedSlots.Contains((DateOnly.FromDateTime(date), time))) continue;

                var slot = new AvailabilitySlot(rule.Id, date, time, time.Add(TimeSpan.FromMinutes(30)));
                await _persistence.Add(slot);
            }
        }
    }

    private static List<(string Canonical, DayOfWeek DayOfWeek, TimeSpan Start, TimeSpan End)> ParseAndValidate(
        AvailabilityModel.Request request)
    {
        var details = new List<(string, string)>();

        if (request.DoctorId == Guid.Empty)
            details.Add((nameof(request.DoctorId), "Es obligatorio."));

        if (request.Days is null || request.Days.Count == 0)
            details.Add((nameof(request.Days), "Debe indicar al menos un día."));

        var result = new List<(string, DayOfWeek, TimeSpan, TimeSpan)>();

        foreach (var day in request.Days ?? [])
        {
            if (!DayNames.TryGetValue(day.Day?.Trim() ?? string.Empty, out var mapped))
            {
                details.Add((nameof(day.Day), $"'{day.Day}' no es un día válido (LUNES a DOMINGO)."));
                continue;
            }

            if (!TimeSpan.TryParse(day.StartTime, out var start) || !TimeSpan.TryParse(day.EndTime, out var end))
            {
                details.Add((nameof(day.StartTime), $"Formato de hora inválido para {day.Day}, use HH:mm."));
                continue;
            }

            if (start >= end)
            {
                details.Add((nameof(day.StartTime), $"El horario de inicio debe ser menor al de fin para {day.Day}."));
                continue;
            }

            result.Add((mapped.Canonical, mapped.DayOfWeek, start, end));
        }

        if (details.Count > 0)
            throw new ValidationException().WithDetail(details);

        return result;
    }

    private static void CheckInternalOverlaps(List<(string Canonical, DayOfWeek DayOfWeek, TimeSpan Start, TimeSpan End)> days)
    {
        foreach (var group in days.GroupBy(d => d.Canonical))
        {
            var list = group.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                for (var j = i + 1; j < list.Count; j++)
                {
                    if (Overlaps(list[i].Start, list[i].End, list[j].Start, list[j].End))
                        throw new ValidationException().WithDetail(
                            nameof(AvailabilityModel.DayRequest.Day),
                            $"Hay horarios solapados para {group.Key} dentro de la misma solicitud.");
                }
            }
        }
    }

    private static bool Overlaps(TimeSpan startA, TimeSpan endA, TimeSpan startB, TimeSpan endB)
        => startA < endB && startB < endA;

    private static string Format(TimeSpan time) => time.ToString(@"hh\:mm");
}