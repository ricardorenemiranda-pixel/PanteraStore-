using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SteamMarket.Api.Contracts;
using SteamMarket.Application.Services.Interfaces;
using SteamMarket.Domain.ValueObjects;

namespace SteamMarket.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly ILogger<AuthController> _logger;
    private readonly IAdminAccountService _adminAccounts;

    public AuthController(
        IConfiguration config,
        IValidator<LoginRequest> loginValidator,
        ILogger<AuthController> logger,
        IAdminAccountService adminAccounts)
    {
        _config = config;
        _loginValidator = loginValidator;
        _logger = logger;
        _adminAccounts = adminAccounts;
    }

    /// <summary>Inicia el login: redirige a Steam y al volver regresa al frontend.</summary>
    [HttpGet("login")]
    public async Task<IActionResult> Login([FromQuery] LoginRequest request, CancellationToken ct)
    {
        var frontend = _config["Frontend:Url"] ?? "http://localhost:4321";

        var validation = await _loginValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            // No hacemos que falle el login: un returnUrl invalido (posible open redirect)
            // simplemente se ignora y volvemos al frontend configurado.
            _logger.LogWarning(
                "returnUrl invalido en /api/auth/login: {ReturnUrl}. Se usa el default.",
                request.ReturnUrl);
        }

        var redirect = validation.IsValid && !string.IsNullOrEmpty(request.ReturnUrl)
            ? request.ReturnUrl!
            : frontend;

        return Challenge(new AuthenticationProperties { RedirectUri = redirect }, "Steam");
    }

    /// <summary>Devuelve el usuario logueado (o authenticated:false).</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Ok(new { authenticated = false });

        var steamId = SteamId.FromOpenIdUrl(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var isAdmin = await _adminAccounts.IsAdminAsync(steamId.Value, ct);
        var isSuperAdmin = _adminAccounts.IsSuperAdmin(steamId.Value);

        return Ok(new
        {
            authenticated = true,
            steamId = steamId.Value,
            name = User.Identity.Name,
            isAdmin,
            isSuperAdmin
        });
    }

    /// <summary>Cierra la sesion.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { ok = true });
    }
}
