using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Dsw2026Tpi.Application.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Application.Services
{
    public class SpecialityService : ISpecialityService
    {
        private readonly IPersistence _persistence;

        public SpecialityService(IPersistence persistence)
        {
            _persistence = persistence;
        }

        public async Task<Pagination<SpecialityModel.Response>> GetAll(int pageSize, int pageIndex, string? name = null)
        {
            var specialities = await _persistence.Paginate<Speciality, string>(
                pageSize,
                pageIndex,
                s => string.IsNullOrWhiteSpace(name) || s.Name.Contains(name),
                s => s.Name);

            return specialities.Map(ToResponse);
        }

        public async Task<SpecialityModel.Response> Create(SpecialityModel.Request request)
        {
            Validate(request);

            var duplicate = await _persistence.First<Speciality>(s => s.Name == request.Name);
            if (duplicate is not null)
                throw new ConflictException(
                    "SPECIALITY_NAME_CONFLICT",
                    $"Ya existe una especialidad con el nombre '{request.Name}'.");

            var speciality = new Speciality(request.Name, request.Description);
            await _persistence.Add(speciality);

            return ToResponse(speciality);
        }

        public async Task<SpecialityModel.Response> Update(Guid id, SpecialityModel.Request request)
        {
            Validate(request);

            var speciality = await _persistence.GetById<Speciality>(id)
                ?? throw new EntityNotFoundException(nameof(Speciality));

            var duplicate = await _persistence.First<Speciality>(s => s.Name == request.Name && s.Id != id);
            if (duplicate is not null)
                throw new ConflictException(
                    "SPECIALITY_NAME_CONFLICT",
                    $"Ya existe una especialidad con el nombre '{request.Name}'.");

            speciality.UpdateDetails(request.Name, request.Description);
            await _persistence.Update(speciality);

            return ToResponse(speciality);
        }

        public async Task Delete(Guid id)
        {
            var speciality = await _persistence.GetById<Speciality>(id)
                ?? throw new EntityNotFoundException(nameof(Speciality));

            await _persistence.SoftDelete(speciality);
        }

        private static void Validate(SpecialityModel.Request request)
        {
            var details = new List<(string, string)>();

            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length < 3 || request.Name.Length > 100)
                details.Add((nameof(request.Name), "Debe tener entre 3 y 100 caracteres."));

            if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length < 10 || request.Description.Length > 100)
                details.Add((nameof(request.Description), "Debe tener entre 10 y 100 caracteres."));

            if (details.Count > 0)
                throw new ValidationException().WithDetail(details);
        }

        private static SpecialityModel.Response ToResponse(Speciality s) => new(s.Id, s.Name, s.Description);
    }
}
