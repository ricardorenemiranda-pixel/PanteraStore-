using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SteamMarket.Application.Services.Interfaces;
using SteamMarket.Domain.ValueObjects;

namespace SteamMarket.Api.Controllers;

[ApiController]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventory;

    public InventoryController(IInventoryService inventory) => _inventory = inventory;

    /// <summary>Items de Dota 2 del usuario logueado. Requiere sesion con Steam.</summary>
    /// <param name="force">
    /// true para forzar un pedido en vivo a Steam ignorando el cache (boton "Actualizar" del
    /// frontend). Sin esto, se sirve desde cache si todavia esta fresco.
    /// </param>
    [HttpGet("dota")]
    public async Task<IActionResult> GetDotaInventory([FromQuery] bool force, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized(new { error = "No has iniciado sesion con Steam." });

        var steamId = SteamId.FromOpenIdUrl(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        if (!steamId.IsValid)
            return Unauthorized(new { error = "No se pudo obtener tu SteamID." });

        var result = await _inventory.GetDotaInventoryAsync(steamId.Value, force, ct);

        if (!result.Success)
            return StatusCode(StatusCodes.Status502BadGateway, new { error = result.Error });

        return Ok(result);
    }
}
