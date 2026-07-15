using Dsw2026Tpi.Application.Interfaces;

namespace Dsw2026Tpi.Application.Services;

/// <summary>
/// Convierte la hora UTC del servidor a hora local de Argentina (UTC-3).
/// Se resuelve el TimeZoneInfo una sola vez (es costoso) y se cachea en un campo estático.
///
/// Nota: el id de zona horaria difiere entre Windows ("Argentina Standard Time") y
/// Linux/macOS ("America/Argentina/Buenos_Aires", formato IANA). Se intenta primero el
/// id de Windows y, si no existe en el sistema operativo actual, se cae al id IANA, para
/// que el mismo código funcione tanto en desarrollo local (Windows) como en CI/contenedores
/// Linux sin tener que tocar nada.
/// </summary>
public class ClockService : IClockService
{
    private static readonly TimeZoneInfo ArgentinaTimeZone = ResolveArgentinaTimeZone();

    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ArgentinaTimeZone);

    private static TimeZoneInfo ResolveArgentinaTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Argentina Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
        }
    }
}