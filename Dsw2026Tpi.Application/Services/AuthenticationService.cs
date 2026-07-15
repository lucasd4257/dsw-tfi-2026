using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Helpers;
using Dsw2026Tpi.CrossCutting.Identity;
using Dsw2026Tpi.CrossCutting.Resources;
using Dsw2026Tpi.Data.Identity;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Dsw2026Tpi.Application.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISignInService _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly JwtService _jwtService;
    private readonly IPersistence _persistence;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(UserManager<ApplicationUser> userManager,
        ISignInService signInManager,
        RoleManager<IdentityRole> roleManager,
        JwtService jwtService,
        IPersistence persistence,
        ILogger<AuthenticationService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _jwtService = jwtService;
        _persistence = persistence;
        _logger = logger;
    }

    public async Task<LoginAdminModel.Response> LoginAdmin(LoginAdminModel.Request request)
    {
        if (!request.Email.IsEmailValid()) throw new AuthenticationException();
        var user = await _userManager.FindByEmailAsync(request.Email) ?? throw new AuthenticationException();
        var result = await _signInManager.CheckPassword(user, request.Password);

        if (!result)
        {
            _logger.LogError("Intento de login fallido para: {Email}", request.Email);
            throw new AuthenticationException();
        }

        var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault();

        var token = _jwtService.GenerateToken(user.UserName!, role);

        return new LoginAdminModel.Response(
            token,
            role
        );
    }

    public async Task<LoginPatientModel.Response> LoginPatient(LoginPatientModel.Request request)
    {
        if (!request.Email.IsEmailValid()) throw new AuthenticationException();

        var dniText = request.Dni.ToString();
        var user = await _userManager.FindByEmailAsync(request.Email);

        Patient? patient;

        if (user is null)
        {
            // RN06: si el paciente inicia sesión por primera vez, se registra automáticamente
            user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // El paciente no se autentica con password (usa email + dni), pero Identity
            // requiere una para crear el usuario. Se genera una interna, aleatoria e inutilizable.
            var internalPassword = $"{Guid.NewGuid():N}Aa1!";
            var createResult = await _userManager.CreateAsync(user, internalPassword);

            if (!createResult.Succeeded)
                throw new ConflictException(nameof(ErrorCodes.REGISTER_USER_CONFLICT), ErrorCodes.REGISTER_USER_CONFLICT)
                    .WithDetail(createResult.Errors.Select(e => (e.Code, e.Description)));

            _ = await _userManager.AddToRoleAsync(user, Roles.Patient);

            // TODO: LoginPatientModel.Request no trae FullName; se usa el email como
            // placeholder. Confirmar con la cátedra si hace falta pedir el nombre en el login
            // o si se completa después mediante otro endpoint.
            patient = new Patient(user.Id, dniText, request.Email);
            await _persistence.Add(patient);

            _logger.LogInformation("Paciente registrado automáticamente: {Email}", request.Email);
        }
        else
        {
            patient = await _persistence.First<Patient>(p => p.ApplicationUserId == user.Id);

            if (patient is null || patient.Dni != dniText)
            {
                _logger.LogError("Intento de login fallido para paciente: {Email}", request.Email);
                throw new AuthenticationException();
            }
        }

        var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault();
        var token = _jwtService.GenerateToken(user.UserName!, role);

        return new LoginPatientModel.Response(token, role);
    }

    public async Task<RegisterModel.Response> Register(RegisterModel.Request request)
    {
        if (!request.Email.IsEmailValid()) throw new ValidationException(ErrorCodes.REGISTER_USER_INVALID,
            nameof(ErrorCodes.REGISTER_USER_INVALID));

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded) throw new ConflictException(nameof(ErrorCodes.REGISTER_USER_CONFLICT),
            ErrorCodes.REGISTER_USER_CONFLICT)
                .WithDetail(result.Errors.Select(e => (e.Code, e.Description)));

        _ = await _userManager.AddToRoleAsync(user, Roles.Administrator);

        _logger.LogInformation("Usuario registrado: {Email}", request.Email);

        return new RegisterModel.Response(request.Email);
    }
}
