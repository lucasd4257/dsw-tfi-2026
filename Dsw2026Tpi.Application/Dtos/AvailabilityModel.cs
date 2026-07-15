namespace Dsw2026Tpi.Application.Dtos;

public record AvailabilityModel
{
    public record DayRequest(string Day, string StartTime, string EndTime);
    public record Request(Guid DoctorId, List<DayRequest> Days);
    public record DayResponse(string Day, string StartTime, string EndTime);
}