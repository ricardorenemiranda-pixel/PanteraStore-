namespace SteamMarket.Infrastructure.Persistence;

/// <summary>Pedido de retiro de saldo hacia Yape/transferencia. El monto se debita del saldo
/// al crear el pedido (para que no se pueda gastar dos veces mientras esta pendiente); si el
/// admin lo rechaza, se le devuelve (ver WalletService.RejectWithdrawalAsync).</summary>
public sealed class WithdrawalRequest
{
    public Guid Id { get; set; }
    public string SteamId64 { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    /// <summary>"Yape" | "Transferencia".</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>Numero de Yape o numero de cuenta/CCI, segun el metodo.</summary>
    public string Destination { get; set; } = string.Empty;

    /// <summary>"Pending" | "Paid" | "Rejected".</summary>
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}
