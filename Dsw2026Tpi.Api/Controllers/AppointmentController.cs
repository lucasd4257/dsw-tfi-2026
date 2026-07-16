using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dsw2026Tpi.Api.Controllers;

[Route("appointments")]
public class AppointmentController : AppController
{
    private readonly IAppointmentService _service;

    public AppointmentController(IAppointmentService service)
    {
        _service = service;
    }

    [HttpPost]
    [Authorize(Policy = Policies.PatientPolicy)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] AppointmentModel.CreateRequest request)
    {
        var email = CurrentUserEmail();
        var result = await _service.Create(request, email);
        return CreatedAtAction(nameof(GetByPatient), new { dni = request.Patient.Dni }, result);
    }

    [HttpGet("patient")]
    [Authorize(Policy = Policies.PatientPolicy)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetByPatient([FromQuery] long dni)
    {
        var email = CurrentUserEmail();
        var result = await _service.GetByPatient(dni, email);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = Policies.PatientPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var email = CurrentUserEmail();
        await _service.Cancel(id, email);
        return NoContent();
    }

    [HttpGet]
    [Authorize(Policy = Policies.AdminPolicy)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByDate([FromQuery] DateTime date)
    {
        var result = await _service.GetByDate(date);
        return Ok(result);
    }

    [HttpGet("search")]
    [Authorize(Policy = Policies.AdminPolicy)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? specialtyId,
        [FromQuery] Guid? doctorId,
        [FromQuery] string? dni,
        [FromQuery] DateTime? date,
        [FromQuery] int pageSize = 10,
        [FromQuery] int pageIndex = 1)
    {
        var result = await _service.Search(specialtyId, doctorId, dni, date, pageSize, pageIndex);
        return Ok(result);
    }

    private string CurrentUserEmail() => User.Identity?.Name ?? throw new AuthenticationException();
}