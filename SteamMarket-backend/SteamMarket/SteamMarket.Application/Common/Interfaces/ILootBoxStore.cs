namespace SteamMarket.Application.Common.Interfaces;

public sealed class LootBoxRecord
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal? MaxItemPrice { get; init; }
    public string? ImageUrl { get; init; }
    public int SortOrder { get; init; }
    public bool IsActive { get; init; }
}

public sealed class LootBoxPoolItemRecord
{
    public Guid Id { get; init; }
    public Guid LootBoxId { get; init; }
    public string MarketHashName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Hero { get; init; }
    public string? Slot { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Rarity { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public int Weight { get; init; }
}

public sealed class LootBoxWinRecord
{
    public Guid Id { get; init; }
    public Guid LootBoxId { get; init; }
    public Guid PoolItemId { get; init; }
    public string SteamId64 { get; init; } = string.Empty;
    public string BotAssetId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime WonAtUtc { get; init; }
    public DateTime? ResolvedAtUtc { get; init; }
}

/// <summary>
/// Persistencia del catalogo de cajas (LootBox + su pool de posibles items), los premios
/// ganados (LootBoxWin) y el contador de fidelidad "compra 9 recibe 1 gratis"
/// (LootBoxPurchaseCounter). La logica de negocio (sortear, cobrar, reservar stock real) vive
/// en LootBoxService; esto es solo el puerto de datos.
/// </summary>
public interface ILootBoxStore
{
    Task<IReadOnlyList<LootBoxRecord>> GetActiveBoxesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<LootBoxRecord>> GetAllBoxesAsync(CancellationToken ct = default);

    Task<LootBoxRecord?> GetBySlugAsync(string slug, CancellationToken ct = default);

    Task<LootBoxRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<LootBoxRecord> UpsertBoxAsync(
        string slug, string name, string category, decimal price, decimal? maxItemPrice, string? imageUrl,
        int sortOrder, bool isActive, CancellationToken ct = default);

    Task<IReadOnlyList<LootBoxPoolItemRecord>> GetPoolItemsAsync(Guid lootBoxId, CancellationToken ct = default);

    Task<LootBoxPoolItemRecord?> GetPoolItemByIdAsync(Guid poolItemId, CancellationToken ct = default);

    Task<LootBoxPoolItemRecord> AddPoolItemAsync(
        Guid lootBoxId, string marketHashName, string displayName, string? hero, string? slot,
        string type, string rarity, string? imageUrl, int weight, CancellationToken ct = default);

    Task RemovePoolItemAsync(Guid poolItemId, CancellationToken ct = default);

    /// <summary>Cuantas compras pagadas lleva ese usuario en esa caja (0 si nunca compro).</summary>
    Task<int> GetPurchaseCountAsync(string steamId64, Guid lootBoxId, CancellationToken ct = default);

    Task IncrementPurchaseCountAsync(string steamId64, Guid lootBoxId, CancellationToken ct = default);

    Task ResetPurchaseCountAsync(string steamId64, Guid lootBoxId, CancellationToken ct = default);

    /// <summary>
    /// Crea el registro de premio, reservando ese BotAssetId para este usuario. Devuelve null si
    /// otra apertura concurrente ya se quedo con ese mismo AssetId (choque contra el indice unico
    /// de BotAssetId) -- en ese caso el llamador debe reintentar con otro item/asset disponible.
    /// </summary>
    Task<LootBoxWinRecord?> TryCreateWinAsync(
        Guid lootBoxId, Guid poolItemId, string steamId64, string botAssetId, CancellationToken ct = default);

    Task<LootBoxWinRecord?> GetWinByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<LootBoxWinRecord>> GetWinsByUserAsync(string steamId64, CancellationToken ct = default);

    /// <summary>AssetIds del bot que ya estan comprometidos con un premio activo (Reserved o
    /// PendingRedeem): hay que excluirlos del stock disponible para no regalar el mismo item
    /// fisico dos veces.</summary>
    Task<IReadOnlyList<string>> GetActivelyClaimedBotAssetIdsAsync(CancellationToken ct = default);

    Task UpdateWinStatusAsync(Guid id, string status, DateTime? resolvedAtUtc, CancellationToken ct = default);
}
