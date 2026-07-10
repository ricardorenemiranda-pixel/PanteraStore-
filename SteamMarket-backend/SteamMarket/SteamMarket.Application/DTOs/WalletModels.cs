namespace SteamMarket.Application.DTOs;

public sealed class WalletTransactionDto
{
    public Guid Id { get; init; }
    public decimal Amount { get; init; }
    public string Type { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class WalletResponse
{
    public decimal Balance { get; init; }
    public IReadOnlyList<WalletTransactionDto> Transactions { get; init; } = Array.Empty<WalletTransactionDto>();
}

public sealed class WithdrawalDto
{
    public Guid Id { get; init; }
    public string SteamId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Method { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? ResolvedAtUtc { get; init; }
}

public sealed class RequestWithdrawalResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public WithdrawalDto? Withdrawal { get; init; }

    public static RequestWithdrawalResult Ok(WithdrawalDto withdrawal) => new() { Success = true, Withdrawal = withdrawal };
    public static RequestWithdrawalResult Fail(string error) => new() { Success = false, Error = error };
}
