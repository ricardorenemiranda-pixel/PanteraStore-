using SteamMarket.Application.DTOs;

namespace SteamMarket.Application.Services.Interfaces;

/// <summary>Contrato del caso de uso de "confirmar venta" (ver nota en IInventoryService sobre por que existe esta interfaz).</summary>
public interface ISellOrderService
{
    /// <summary>Crea una orden a partir de los assetId seleccionados, re-cotizando desde el
    /// inventario real (nunca confia en un total mandado por el cliente).</summary>
    Task<CreateSellOrderResult> CreateOrderAsync(string steamId64, IReadOnlyList<string> assetIds, CancellationToken ct = default);

    Task<IReadOnlyList<SellOrderDto>> GetMyOrdersAsync(string steamId64, CancellationToken ct = default);

    Task<IReadOnlyList<SellOrderDto>> GetPendingOrdersAsync(CancellationToken ct = default);

    /// <returns>false si la orden no existe o ya no esta Pending.</returns>
    Task<bool> CompleteOrderAsync(Guid orderId, CancellationToken ct = default);

    /// <returns>false si la orden no existe o ya no esta Pending.</returns>
    Task<bool> RejectOrderAsync(Guid orderId, string? note, CancellationToken ct = default);
}
