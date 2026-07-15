namespace SteamMarket.Infrastructure.Persistence;

/// <summary>
/// Un premio ganado por un usuario al abrir una caja. BotAssetId es el item REAL (existe de
/// verdad en el inventario de Steam de la cuenta bot) que quedo reservado para este usuario:
/// se resuelve contra el stock real recien en el momento de abrir, no antes (ver
/// LootBoxService.OpenBoxAsync), para nunca prometer un item que la cuenta bot no tiene.
/// </summary>
public sealed class LootBoxWin
{
    public Guid Id { get; set; }
    public Guid LootBoxId { get; set; }
    public Guid PoolItemId { get; set; }
    public string SteamId64 { get; set; } = string.Empty;

    /// <summary>AssetId real en el inventario de Steam del bot, reservado para este usuario.</summary>
    public string BotAssetId { get; set; } = string.Empty;

    /// <summary>
    /// "Reserved" (recien ganado, el usuario todavia no decidio que hacer) |
    /// "PendingRedeem" (pidio el canje, el bot ya mando el trade de entrega, esperando que lo
    /// acepte en Steam) | "Sold" (lo cambio por saldo en la billetera) | "Redeemed" (lo recibio
    /// de verdad en su cuenta de Steam).
    /// </summary>
    public string Status { get; set; } = "Reserved";

    public DateTime WonAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}
