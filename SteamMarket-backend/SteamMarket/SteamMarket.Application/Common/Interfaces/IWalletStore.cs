namespace SteamMarket.Application.Common.Interfaces;

public sealed class WalletTransactionRecord
{
    public Guid Id { get; init; }
    public decimal Amount { get; init; }
    public string Type { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class WithdrawalRecord
{
    public Guid Id { get; init; }
    public string SteamId64 { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Method { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? ResolvedAtUtc { get; init; }
}

/// <summary>
/// Puerto hacia la billetera interna: saldo, movimientos y pedidos de retiro. El saldo nunca
/// se edita directo: siempre se mueve via CreditAsync/TryDebitAsync para que quede un registro
/// (WalletTransaction) de cada cambio.
/// </summary>
public interface IWalletStore
{
    Task<decimal> GetBalanceAsync(string steamId64, CancellationToken ct = default);

    Task<IReadOnlyList<WalletTransactionRecord>> GetTransactionsAsync(string steamId64, CancellationToken ct = default);

    Task CreditAsync(string steamId64, decimal amount, string type, string? relatedId, string? description, CancellationToken ct = default);

    /// <returns>false si el saldo era insuficiente (no se hizo nada).</returns>
    Task<bool> TryDebitAsync(string steamId64, decimal amount, string type, string? relatedId, string? description, CancellationToken ct = default);

    Task<WithdrawalRecord> CreateWithdrawalAsync(string steamId64, decimal amount, string method, string destination, CancellationToken ct = default);

    Task<IReadOnlyList<WithdrawalRecord>> GetWithdrawalsAsync(string steamId64, CancellationToken ct = default);

    Task<IReadOnlyList<WithdrawalRecord>> GetPendingWithdrawalsAsync(CancellationToken ct = default);

    Task<WithdrawalRecord?> GetWithdrawalByIdAsync(Guid id, CancellationToken ct = default);

    Task UpdateWithdrawalStatusAsync(Guid id, string status, CancellationToken ct = default);
}
