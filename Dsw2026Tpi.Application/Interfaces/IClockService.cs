namespace Dsw2026Tpi.Application.Interfaces;

/// <summary>
/// Decisión de diseño: toda la aplicación maneja fechas y horas en hora local de
/// Argentina (UTC-3), no en UTC. Este servicio centraliza el cálculo de "ahora" para
/// que todos los módulos (Disponibilidades, Citas, etc.) usen la misma fuente de verdad
/// y evitar inconsistencias cerca de la medianoche.
/// </summary>
public interface IClockService
{
    DateTime Now { get; }
}
