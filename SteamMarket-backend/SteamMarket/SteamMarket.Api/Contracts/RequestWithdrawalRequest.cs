namespace SteamMarket.Api.Contracts;

/// <summary>Body de POST /api/wallet/withdrawals.</summary>
public sealed class RequestWithdrawalRequest
{
    public decimal Amount { get; init; }

    /// <summary>"Yape" o "Transferencia".</summary>
    public string Method { get; init; } = string.Empty;

    /// <summary>Numero de Yape o numero de cuenta/CCI, segun el metodo.</summary>
    public string Destination { get; init; } = string.Empty;
}
