using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Data.Identity;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace Dsw2026Tpi.Application.Services;

public class AppointmentService : IAppointmentService
{
    private readonly IPersistence _persistence;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IClockService _clockService;

    private const string IncludePath = "AvailabilitySlot.AvailabilityRule.Doctor.Speciality";
    private const string IncludePatient = "Patient";

    public AppointmentService(IPersistence persistence, UserManager<ApplicationUser> userManager, IClockService clockService)
    {
        _persistence = persistence;
        _userManager = userManager;
        _clockService = clockService;
    }

    public async Task<AppointmentModel.Response> Create(AppointmentModel.CreateRequest request, string currentUserEmail)
    {
        Validate(request);

        var patient = await GetCurrentPatientAsync(currentUserEmail);

        // Decisión de diseño (Opción A): el paciente autenticado solo puede reservar para
        // sí mismo. Si el DNI del request no coincide con el paciente resuelto desde el
        // token, se rechaza aunque el DNI pertenezca a otro paciente real del sistema.
        if (patient.Dni != request.Patient.Dni.ToString())
            throw new AuthorizationException();

        _ = await _persistence.GetById<Doctor>(request.DoctorId)
            ?? throw new EntityNotFoundException(nameof(Doctor));

        var slot = await _persistence.GetById<AvailabilitySlot>(request.AvailabilityId, nameof(AvailabilitySlot.AvailabilityRule))
            ?? throw new EntityNotFoundException(nameof(AvailabilitySlot));

        if (slot.AvailabilityRule?.DoctorId != request.DoctorId)
            throw new ValidationException().WithDetail(nameof(request.DoctorId), "El turno no pertenece al médico indicado.");

        if (slot.Status != AvailabilitySlotStatus.AVAILABLE || !slot.IsActive)
            throw new ConflictException("APPOINTMENT_CONFLICT", "Slot already booked")
                .WithDetail("dateTime", "slot_inavailable");

        // RN04: no se pueden reservar turnos en fechas u horarios pasados.
        // Se usa hora local de Argentina (IClockService), no UTC, según la decisión de diseño global.
        var slotDateTime = slot.SlotDate.Date + slot.StartTime;
        if (slotDateTime < _clockService.Now)
            throw new ValidationException().WithDetail(nameof(request.AvailabilityId), "No se pueden reservar turnos en el pasado.");

        // RN03 / control de concurrencia: se re-chequea que no exista ya una cita BOOKED
        // sobre este slot justo antes de reservar, para reducir la ventana de doble reserva.
        // Nota: el índice único sobre Appointments.AvailabilitySlotId actúa como red de
        // seguridad final a nivel de base de datos ante una carrera que igual ocurra entre
        // este chequeo y el Add. Como IPersistence no expone hacia Application las
        // excepciones de EF Core (para no romper la separación en capas), esa carrera
        // residual no se mapea acá a un 409 explícito; queda documentada como limitación
        // conocida del alcance de este TP.
        var existingBooked = await _persistence.First<Appointment>(
            a => a.AvailabilitySlotId == slot.Id && a.Status == AppointmentStatus.BOOKED);

        if (existingBooked is not null)
            throw new ConflictException("APPOINTMENT_CONFLICT", "Slot already booked")
                .WithDetail("dateTime", "slot_inavailable");

        var appointment = new Appointment(slot.Id, patient.Id, request.Reason);
        await _persistence.Add(appointment);

        slot.ChangeStatus(AvailabilitySlotStatus.BOOKED);
        await _persistence.Update(slot);

        var created = await _persistence.GetById<Appointment>(appointment.Id, IncludePath, IncludePatient);
        return ToResponse(created!);
    }

    public async Task<IEnumerable<AppointmentModel.Response>> GetByPatient(long dni, string currentUserEmail)
    {
        var patient = await GetCurrentPatientAsync(currentUserEmail);

        // Decisión de diseño (Opción A): el paciente solo puede consultar sus propios turnos.
        if (patient.Dni != dni.ToString())
            throw new AuthorizationException();

        // RF: "retorna solo turnos activos (no históricos)". Se interpreta como estado
        // BOOKED únicamente; CANCELLED, ATTENDED y NO_SHOW quedan afuera por ser estados finales.
        var appointments = await _persistence.GetFiltered<Appointment>(
            a => a.PatientId == patient.Id && a.Status == AppointmentStatus.BOOKED,
            IncludePath, IncludePatient);

        return (appointments ?? []).Select(ToResponse);
    }

    public async Task Cancel(Guid id, string currentUserEmail)
    {
        var patient = await GetCurrentPatientAsync(currentUserEmail);

        var appointment = await _persistence.GetById<Appointment>(id)
            ?? throw new EntityNotFoundException(nameof(Appointment));

        // Decisión de diseño (Opción A): el paciente solo puede cancelar sus propios turnos.
        if (appointment.PatientId != patient.Id)
            throw new AuthorizationException();

        // RF08 / CU03: solo se puede cancelar si el turno está en estado BOOKED.
        if (appointment.Status != AppointmentStatus.BOOKED)
            throw new ConflictException("APPOINTMENT_NOT_CANCELLABLE", "Solo se puede cancelar un turno en estado BOOKED.");

        appointment.Cancel();
        await _persistence.Update(appointment);

        var slot = await _persistence.GetById<AvailabilitySlot>(appointment.AvailabilitySlotId)
            ?? throw new EntityNotFoundException(nameof(AvailabilitySlot));

        slot.ChangeStatus(AvailabilitySlotStatus.AVAILABLE);
        await _persistence.Update(slot);
    }

    public async Task<IEnumerable<AppointmentModel.Response>> GetByDate(DateTime date)
    {
        var appointments = await _persistence.GetFiltered<Appointment>(
            a => a.AvailabilitySlot!.SlotDate.Date == date.Date,
            IncludePath, IncludePatient);

        return (appointments ?? []).Select(ToResponse);
    }

    public async Task<Pagination<AppointmentModel.Response>> Search(
        Guid? specialityId, Guid? doctorId, string? dni, DateTime? date, int pageSize, int pageIndex)
    {
        var result = await _persistence.Paginate<Appointment, DateTime>(
            pageSize,
            pageIndex,
            a =>
                (specialityId == null || a.AvailabilitySlot!.AvailabilityRule!.Doctor!.SpecialityId == specialityId) &&
                (doctorId == null || a.AvailabilitySlot!.AvailabilityRule!.DoctorId == doctorId) &&
                (string.IsNullOrWhiteSpace(dni) || a.Patient!.Dni == dni) &&
                (date == null || a.AvailabilitySlot!.SlotDate.Date == date.Value.Date),
            a => a.AvailabilitySlot!.SlotDate,
            IncludePath, IncludePatient);

        return result.Map(ToResponse);
    }

    private async Task<Patient> GetCurrentPatientAsync(string? currentUserEmail)
    {
        if (string.IsNullOrWhiteSpace(currentUserEmail)) throw new AuthenticationException();

        var user = await _userManager.FindByEmailAsync(currentUserEmail) ?? throw new AuthenticationException();

        var patient = await _persistence.First<Patient>(p => p.ApplicationUserId == user.Id)
            ?? throw new AuthenticationException();

        return patient;
    }

    private static void Validate(AppointmentModel.CreateRequest request)
    {
        var details = new List<(string, string)>();

        if (request.DoctorId == Guid.Empty)
            details.Add((nameof(request.DoctorId), "Es obligatorio."));

        if (request.AvailabilityId == Guid.Empty)
            details.Add((nameof(request.AvailabilityId), "Es obligatorio."));

        if (request.Patient is null || request.Patient.Dni <= 0)
            details.Add((nameof(request.Patient), "El DNI del paciente es obligatorio."));

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length < 5)
            details.Add((nameof(request.Reason), "Debe tener al menos 5 caracteres."));

        if (details.Count > 0)
            throw new ValidationException().WithDetail(details);
    }

    private static AppointmentModel.Response ToResponse(Appointment a)
    {
        var slot = a.AvailabilitySlot!;
        var rule = slot.AvailabilityRule!;
        var doctor = rule.Doctor!;
        var patient = a.Patient!;

        return new AppointmentModel.Response(
            a.Id,
            doctor.Id,
            doctor.Name,
            doctor.Speciality?.Name ?? string.Empty,
            patient.Dni,
            patient.FullName,
            slot.SlotDate,
            slot.StartTime.ToString(@"hh\:mm"),
            slot.EndTime.ToString(@"hh\:mm"),
            a.Status.ToString(),
            a.Reason);
    }
}