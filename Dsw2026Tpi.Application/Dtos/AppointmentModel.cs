namespace Dsw2026Tpi.Application.Dtos;

public record AppointmentModel
{
    public record PatientRequest(long Dni);

    public record CreateRequest(Guid DoctorId, Guid AvailabilityId, PatientRequest Patient, string Reason);

    public record Response(
        Guid Id,
        Guid DoctorId,
        string DoctorName,
        string SpecialityName,
        string PatientDni,
        string PatientName,
        DateTime SlotDate,
        string StartTime,
        string EndTime,
        string Status,
        string Reason);
}