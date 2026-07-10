using Microsoft.AspNetCore.Mvc;
using SteamMarket.Api.Contracts;
using SteamMarket.Application.Services.Interfaces;

namespace SteamMarket.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : SteamAuthControllerBase
{
    private readonly ISellOrderService _orders;

    public OrdersController(ISellOrderService orders) => _orders = orders;

    /// <summary>Confirma la venta de los items seleccionados: re-cotiza server-side, valida el
    /// monto minimo y crea la orden. Devuelve la Trade URL de la cuenta almacen.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSellOrderRequest request, CancellationToken ct)
    {
        if (!TryGetSteamId(out var steamId64, out var unauthorized))
            return unauthorized!;

        var result = await _orders.CreateOrderAsync(steamId64, request.AssetIds, ct);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Ok(new
        {
            success = true,
            order = result.Order,
            adminTradeUrl = result.AdminTradeUrl,
            botOfferSent = result.BotOfferSent,
            botOfferError = result.BotOfferError
        });
    }

    /// <summary>Historial de ordenes del usuario logueado.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        if (!TryGetSteamId(out var steamId64, out var unauthorized))
            return unauthorized!;

        var orders = await _orders.GetMyOrdersAsync(steamId64, ct);
        return Ok(orders);
    }
}
