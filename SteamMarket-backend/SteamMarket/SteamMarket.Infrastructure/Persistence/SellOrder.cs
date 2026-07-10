namespace SteamMarket.Infrastructure.Persistence;

/// <summary>
/// Una "venta" que el usuario confirmo desde su carrito: la promesa de que va a mandar esos
/// items por trade a la cuenta almacen. Queda "Pending" hasta que el admin confirma que
/// recibio el trade real en Steam; recien ahi se acredita el saldo (ver SellOrderService).
/// </summary>
public sealed class SellOrder
{
    public Guid Id { get; set; }
    public string SteamId64 { get; set; } = string.Empty;

    /// <summary>Snapshot (JSON) de los items al momento de confirmar: [{assetId,name,iconUrl,payoutPrice}].
    /// Se guarda una copia porque el inventario real de Steam puede cambiar o el item puede
    /// dejar de estar disponible antes de que el admin revise la orden.</summary>
    public string ItemsJson { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    /// <summary>"Pending" | "Completed" | "Rejected".</summary>
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? AdminNote { get; set; }
}
