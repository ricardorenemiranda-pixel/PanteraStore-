namespace SteamMarket.Infrastructure.Persistence;

/// <summary>
/// Copia persistida (SQLite) del ultimo inventario de Dota obtenido de Steam para un usuario.
/// Igual que CachedMarketPrice, pero para el inventario completo: sin esto, cada carga de
/// pagina forzaria un fetch en vivo a Steam, y el endpoint de inventario rate-limita agresivo.
/// </summary>
public sealed class CachedInventory
{
    public string SteamId64 { get; set; } = string.Empty;

    /// <summary>Los InventoryItem serializados como JSON (lista completa).</summary>
    public string ItemsJson { get; set; } = string.Empty;

    public DateTime FetchedAtUtc { get; set; }
}
