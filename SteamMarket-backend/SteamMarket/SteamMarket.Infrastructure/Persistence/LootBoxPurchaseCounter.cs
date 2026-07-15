namespace SteamMarket.Infrastructure.Persistence;

/// <summary>
/// Contador de "pity" por usuario y por caja: cada 9 compras PAGADAS de la misma caja, la
/// decima es gratis (igual que el "0/9 · Compra 9 Recibe 1 gratis" de GiorDota). Se resetea a 0
/// despues de canjear la gratis.
/// </summary>
public sealed class LootBoxPurchaseCounter
{
    public Guid Id { get; set; }
    public string SteamId64 { get; set; } = string.Empty;
    public Guid LootBoxId { get; set; }
    public int Count { get; set; }
}
