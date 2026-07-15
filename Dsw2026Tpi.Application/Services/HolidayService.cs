using Dsw2026Tpi.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace Dsw2026Tpi.Application.Services;

/// <summary>
/// Decisión de diseño (3): la lista de feriados se carga una sola vez desde
/// appsettings.json (sección "Holidays", formato "yyyy-MM-dd") y se registra como
/// Singleton en el DI, ya que no se espera que cambie en caliente durante la ejecución.
/// Si se necesitara actualizarla sin reiniciar la app, habría que leer IConfiguration
/// en cada llamada en lugar de cachear el HashSet en el constructor.
/// </summary>
public class HolidayService : IHolidayService
{
    private readonly HashSet<DateTime> _holidays;

    public HolidayService(IConfiguration configuration)
    {
        var rawDates = configuration.GetSection("Holidays").Get<string[]>() ?? [];

        _holidays = rawDates
            .Select(d => DateTime.ParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture).Date)
            .ToHashSet();
    }

    public bool IsHoliday(DateTime date) => _holidays.Contains(date.Date);
}