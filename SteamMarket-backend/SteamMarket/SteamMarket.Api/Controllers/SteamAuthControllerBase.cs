using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SteamMarket.Domain.ValueObjects;

namespace SteamMarket.Api.Controllers;

/// <summary>
/// Controllers que necesitan el SteamID64 del usuario logueado heredan de aca para no repetir
/// la misma extraccion/validacion de claims en cada uno (ProfileController, OrdersController,
/// WalletController, etc).
/// </summary>
public abstract class SteamAuthControllerBase : ControllerBase
{
    protected bool TryGetSteamId(out string steamId64, out IActionResult? unauthorized)
    {
        steamId64 = string.Empty;

        if (User.Identity?.IsAuthenticated != true)
        {
            unauthorized = Unauthorized(new { error = "No has iniciado sesion con Steam." });
            return false;
        }

        var steamId = SteamId.FromOpenIdUrl(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        if (!steamId.IsValid)
        {
            unauthorized = Unauthorized(new { error = "No se pudo obtener tu SteamID." });
            return false;
        }

        steamId64 = steamId.Value;
        unauthorized = null;
        return true;
    }

    /// <summary>Igual que TryGetSteamId pero sin forzar 401: para endpoints publicos donde el
    /// login es opcional (ej. el detalle de una caja muestra el contador de fidelidad solo si
    /// hay sesion, pero cualquiera puede ver el contenido).</summary>
    protected string? TryGetSteamIdOptional()
    {
        if (User.Identity?.IsAuthenticated != true) return null;

        var steamId = SteamId.FromOpenIdUrl(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        return steamId.IsValid ? steamId.Value : null;
    }
}
