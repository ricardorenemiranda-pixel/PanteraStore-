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
}
