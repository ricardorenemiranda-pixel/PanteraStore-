namespace SteamMarket.Application.Common.Interfaces;

public sealed class SellOrderItem
{
    public string AssetId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? IconUrl { get; init; }
    public decimal PayoutPrice { get; init; }
}

public sealed class SellOrderRecord
{
    public Guid Id { get; init; }
    public string SteamId64 { get; init; } = string.Empty;
    public IReadOnlyList<SellOrderItem> Items { get; init; } = Array.Empty<SellOrderItem>();
    public decimal TotalAmount { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? ResolvedAtUtc { get; init; }
    public string? AdminNote { get; init; }
}

/// <summary>Puerto hacia la persistencia de ordenes de venta ("confirmar venta" desde el carrito).</summary>
public interface ISellOrderStore
{
    Task<SellOrderRecord> CreateAsync(string steamId64, IReadOnlyList<SellOrderItem> items, decimal totalAmount, CancellationToken ct = default);

    Task<IReadOnlyList<SellOrderRecord>> GetByUserAsync(string steamId64, CancellationToken ct = default);

    Task<IReadOnlyList<SellOrderRecord>> GetPendingAsync(CancellationToken ct = default);

    Task<SellOrderRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task UpdateStatusAsync(Guid id, string status, string? adminNote, CancellationToken ct = default);
}
