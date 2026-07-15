namespace Dsw2026Tpi.Application.Interfaces;

/// <summary>
/// Decisión de diseño (3): los días feriados/no laborales se resuelven con una lista simple
/// configurada en appsettings.json (sección "Holidays"), no se implementa un calendario
/// dinámico ni integración con un proveedor externo de feriados.
/// </summary>
public interface IHolidayService
{
    bool IsHoliday(DateTime date);
}