using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Domain.Entities;

namespace Dsw2026Tpi.Application.Interfaces;

public interface IAppointmentService
{
    Task<AppointmentModel.Response> Create(AppointmentModel.CreateRequest request, string currentUserEmail);
    Task<IEnumerable<AppointmentModel.Response>> GetByPatient(long dni, string currentUserEmail);
    Task Cancel(Guid id, string currentUserEmail);
    Task<IEnumerable<AppointmentModel.Response>> GetByDate(DateTime date);
    Task<Pagination<AppointmentModel.Response>> Search(Guid? specialityId, Guid? doctorId, string? dni, DateTime? date, int pageSize, int pageIndex);
}