using SteamMarket.Application.DTOs;

namespace SteamMarket.Application.Services.Interfaces;

/// <summary>Contrato del caso de uso de billetera (saldo, movimientos, retiros).</summary>
public interface IWalletService
{
    Task<WalletResponse> GetWalletAsync(string steamId64, CancellationToken ct = default);

    Task<RequestWithdrawalResult> RequestWithdrawalAsync(string steamId64, decimal amount, string method, string destination, CancellationToken ct = default);

    Task<IReadOnlyList<WithdrawalDto>> GetMyWithdrawalsAsync(string steamId64, CancellationToken ct = default);

    Task<IReadOnlyList<WithdrawalDto>> GetPendingWithdrawalsAsync(CancellationToken ct = default);

    /// <returns>false si el retiro no existe o ya no esta Pending.</returns>
    Task<bool> MarkWithdrawalPaidAsync(Guid id, CancellationToken ct = default);

    /// <returns>false si el retiro no existe o ya no esta Pending. Si tiene exito, le devuelve el saldo al usuario.</returns>
    Task<bool> RejectWithdrawalAsync(Guid id, CancellationToken ct = default);
}
