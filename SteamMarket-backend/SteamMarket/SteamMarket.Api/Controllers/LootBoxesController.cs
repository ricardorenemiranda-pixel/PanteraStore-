using Microsoft.AspNetCore.Mvc;
using SteamMarket.Application.Services.Interfaces;

namespace SteamMarket.Api.Controllers;

/// <summary>
/// Endpoints publicos de cajas (loot boxes), igual en logica e interfaz a
/// giordota.com/cajas. Cualquiera puede ver el catalogo y el contenido de una caja sin sesion;
/// abrir una caja, pedir la "prueba gratis" o resolver un premio (vender/canjear) requiere login.
/// </summary>
[ApiController]
[Route("api/lootboxes")]
public class LootBoxesController : SteamAuthControllerBase
{
    private readonly ILootBoxService _lootBoxes;

    public LootBoxesController(ILootBoxService lootBoxes) => _lootBoxes = lootBoxes;

    [HttpGet]
    public async Task<IActionResult> GetBoxes(CancellationToken ct) =>
        Ok(await _lootBoxes.GetBoxesAsync(ct));

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBoxDetail(string slug, CancellationToken ct)
    {
        var detail = await _lootBoxes.GetBoxDetailAsync(slug, TryGetSteamIdOptional(), ct);
        if (detail is null) return NotFound(new { error = "Esa caja no existe." });
        return Ok(detail);
    }

    [HttpPost("{slug}/demo")]
    public async Task<IActionResult> Demo(string slug, CancellationToken ct)
    {
        if (!TryGetSteamId(out _, out var unauthorized)) return unauthorized!;

        var result = await _lootBoxes.DemoOpenAsync(slug, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return Ok(new { success = true, item = result.Item });
    }

    [HttpPost("{slug}/open")]
    public async Task<IActionResult> Open(string slug, CancellationToken ct)
    {
        if (!TryGetSteamId(out var steamId64, out var unauthorized)) return unauthorized!;

        var result = await _lootBoxes.OpenBoxAsync(steamId64, slug, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });

        return Ok(new
        {
            success = true,
            win = result.Win,
            wasFree = result.WasFree,
            pityCount = result.PityCount,
        });
    }

    [HttpGet("me/wins")]
    public async Task<IActionResult> GetMyWins(CancellationToken ct)
    {
        if (!TryGetSteamId(out var steamId64, out var unauthorized)) return unauthorized!;
        return Ok(await _lootBoxes.GetMyWinsAsync(steamId64, ct));
    }

    [HttpPost("wins/{id:guid}/sell")]
    public async Task<IActionResult> SellWin(Guid id, CancellationToken ct)
    {
        if (!TryGetSteamId(out var steamId64, out var unauthorized)) return unauthorized!;

        var result = await _lootBoxes.SellWinAsync(steamId64, id, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return Ok(new { success = true, creditedAmount = result.CreditedAmount });
    }

    [HttpPost("wins/{id:guid}/redeem")]
    public async Task<IActionResult> RedeemWin(Guid id, CancellationToken ct)
    {
        if (!TryGetSteamId(out var steamId64, out var unauthorized)) return unauthorized!;

        var result = await _lootBoxes.RedeemWinAsync(steamId64, id, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return Ok(new { success = true });
    }
}
