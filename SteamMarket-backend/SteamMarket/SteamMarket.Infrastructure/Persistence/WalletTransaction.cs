namespace SteamMarket.Infrastructure.Persistence;

/// <summary>
/// Un movimiento de la billetera interna de un usuario. Es el "libro mayor": UserProfile.Balance
/// es solo un cache derivado de la suma de estos movimientos, para no tener que sumarlos en
/// cada lectura.
/// </summary>
public sealed class WalletTransaction
{
    public Guid Id { get; set; }
    public string SteamId64 { get; set; } = string.Empty;

    /// <summary>Positivo = credito (ej. venta completada), negativo = debito (ej. retiro).</summary>
    public decimal Amount { get; set; }

    /// <summary>"Sale" | "Withdrawal" | "WithdrawalRefund" | "Adjustment".</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Id de la orden de venta o del retiro relacionado, si aplica.</summary>
    public string? RelatedId { get; set; }

    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
