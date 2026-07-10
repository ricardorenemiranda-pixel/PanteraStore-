namespace SteamMarket.Api.Contracts;

/// <summary>
/// Query params de GET /api/auth/login. Se valida con LoginRequestValidator
/// (FluentValidation) antes de usar ReturnUrl para el redirect post-login.
/// </summary>
public sealed class LoginRequest
{
    public string? ReturnUrl { get; init; }
}
