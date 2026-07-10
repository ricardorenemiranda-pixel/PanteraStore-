using FluentValidation;
using SteamMarket.Api.Contracts;

namespace SteamMarket.Api.Validation;

/// <summary>
/// Evita "open redirect": sin esto, alguien podria mandar un link tipo
/// /api/auth/login?returnUrl=https://sitio-malicioso.com y, despues de loguearse
/// de verdad con Steam, terminar redirigido a un sitio de phishing.
///
/// Solo se permite:
/// 1) No mandar returnUrl (se usa el Frontend:Url configurado).
/// 2) Una ruta relativa (empieza con "/", pero no "//" que un navegador puede
///    interpretar como protocol-relative hacia otro host).
/// 3) Una URL absoluta cuyo origen (scheme+host+port) sea EXACTAMENTE el
///    Frontend:Url configurado en appsettings.json.
/// </summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator(IConfiguration config)
    {
        var allowedOrigin = config["Frontend:Url"] ?? "http://localhost:4321";

        RuleFor(x => x.ReturnUrl)
            .Must(returnUrl => IsSafeReturnUrl(returnUrl, allowedOrigin))
            .WithMessage("returnUrl debe ser una ruta relativa o apuntar al origen del frontend configurado.");
    }

    private static bool IsSafeReturnUrl(string? returnUrl, string allowedOrigin)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return true; // no mandar nada es valido, se usa el default

        if (returnUrl.StartsWith('/') && !returnUrl.StartsWith("//"))
            return true; // ruta relativa dentro del mismo frontend

        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri) &&
            Uri.TryCreate(allowedOrigin, UriKind.Absolute, out var allowedUri))
        {
            return uri.Scheme == allowedUri.Scheme
                && uri.Host == allowedUri.Host
                && uri.Port == allowedUri.Port;
        }

        return false;
    }
}
