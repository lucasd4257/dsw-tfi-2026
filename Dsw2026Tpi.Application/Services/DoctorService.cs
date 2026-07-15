using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;

namespace Dsw2026Tpi.Application.Services;

public class DoctorService : IDoctorService
{
    private readonly IPersistence _persistence;

    public DoctorService(IPersistence persistence)
    {
        _persistence = persistence;
    }

    public async Task<Pagination<DoctorModel.Response>> GetAll(int pageSize, int pageIndex, string? name = null)
    {
        var doctors = await _persistence.Paginate<Doctor, string>(pageSize, pageIndex, d => string.IsNullOrWhiteSpace(name) ||
                                                   d.Name.Contains(name), x => x.Name, nameof(Doctor.Speciality));

        return doctors.Map(ToResponse);
    }

    public async Task<DoctorModel.Response> Create(DoctorModel.Request request)
    {
        Validate(request);

        var speciality = await _persistence.GetById<Speciality>(request.SpecialityId)
            ?? throw new EntityNotFoundException(nameof(Speciality));

        var duplicateLicense = await _persistence.First<Doctor>(d => d.LicenseNumber == request.LicenseNumber);
        if (duplicateLicense is not null)
            throw new ConflictException(
                "DOCTOR_LICENSE_CONFLICT",
                $"Ya existe un médico con la matrícula '{request.LicenseNumber}'.");

        var doctor = new Doctor(request.Name, request.LicenseNumber, speciality);
        await _persistence.Add(doctor);

        return ToResponse(doctor);
    }

    public async Task<DoctorModel.Response> Update(Guid id, DoctorModel.Request request)
    {
        Validate(request);

        var doctor = await _persistence.GetById<Doctor>(id, nameof(Doctor.Speciality))
            ?? throw new EntityNotFoundException(nameof(Doctor));

        var speciality = await _persistence.GetById<Speciality>(request.SpecialityId)
            ?? throw new EntityNotFoundException(nameof(Speciality));

        var duplicateLicense = await _persistence.First<Doctor>(d => d.LicenseNumber == request.LicenseNumber && d.Id != id);
        if (duplicateLicense is not null)
            throw new ConflictException(
                "DOCTOR_LICENSE_CONFLICT",
                $"Ya existe un médico con la matrícula '{request.LicenseNumber}'.");

        doctor.UpdateDetails(request.Name, request.LicenseNumber, speciality);
        await _persistence.Update(doctor);

        return ToResponse(doctor);
    }

    public async Task Delete(Guid id)
    {
        var doctor = await _persistence.GetById<Doctor>(id)
            ?? throw new EntityNotFoundException(nameof(Doctor));

        await _persistence.SoftDelete(doctor);
    }

    private static void Validate(DoctorModel.Request request)
    {
        var details = new List<(string, string)>();

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length < 3 || request.Name.Length > 100)
            details.Add((nameof(request.Name), "Debe tener entre 3 y 100 caracteres."));

        if (string.IsNullOrWhiteSpace(request.LicenseNumber))
            details.Add((nameof(request.LicenseNumber), "Es obligatorio."));

        if (request.SpecialityId == Guid.Empty)
            details.Add((nameof(request.SpecialityId), "Es obligatorio."));

        if (details.Count > 0)
            throw new ValidationException().WithDetail(details);
    }

    private static DoctorModel.Response ToResponse(Doctor d) => new(d.Id, d.Name, d.LicenseNumber,
        new DoctorModel.SpecialityDto(d.Speciality?.Id, d.Speciality?.Name));
}