using SteamMarket.Application.Common.Interfaces;
using SteamMarket.Application.DTOs;
using SteamMarket.Application.Services.Interfaces;

namespace SteamMarket.Application.Services;

/// <summary>
/// Caso de uso: leer la billetera y pedir/gestionar retiros.
/// El monto de un retiro se debita del saldo AL MOMENTO de pedirlo (no al pagarlo), para que
/// el usuario no pueda gastarlo dos veces mientras esta pendiente. Si el admin rechaza el
/// retiro, se le devuelve el saldo (RejectWithdrawalAsync).
/// </summary>
public sealed class WalletService : IWalletService
{
    private static readonly string[] AllowedMethods = { "Yape", "Transferencia" };

    private readonly IWalletStore _wallet;

    public WalletService(IWalletStore wallet) => _wallet = wallet;

    public async Task<WalletResponse> GetWalletAsync(string steamId64, CancellationToken ct = default)
    {
        var balance = await _wallet.GetBalanceAsync(steamId64, ct);
        var transactions = await _wallet.GetTransactionsAsync(steamId64, ct);

        return new WalletResponse
        {
            Balance = balance,
            Transactions = transactions.Select(t => new WalletTransactionDto
            {
                Id = t.Id,
                Amount = t.Amount,
                Type = t.Type,
                Description = t.Description,
                CreatedAtUtc = t.CreatedAtUtc
            }).ToList()
        };
    }

    public async Task<RequestWithdrawalResult> RequestWithdrawalAsync(string steamId64, decimal amount, string method, string destination, CancellationToken ct = default)
    {
        if (amount <= 0)
            return RequestWithdrawalResult.Fail("El monto debe ser mayor a 0.");

        if (string.IsNullOrWhiteSpace(destination))
            return RequestWithdrawalResult.Fail("Falta el numero de Yape o la cuenta de destino.");

        if (!AllowedMethods.Contains(method))
            return RequestWithdrawalResult.Fail("Metodo de retiro invalido.");

        // Se crea el pedido primero (para tener su Id) y recien despues se debita el saldo
        // referenciando ese Id; si el debito falla por saldo insuficiente, se marca el pedido
        // como rechazado en vez de dejarlo huerfano.
        var record = await _wallet.CreateWithdrawalAsync(steamId64, amount, method, destination, ct);

        var debited = await _wallet.TryDebitAsync(steamId64, amount, "Withdrawal", record.Id.ToString(), $"Retiro via {method}", ct);
        if (!debited)
        {
            await _wallet.UpdateWithdrawalStatusAsync(record.Id, "Rejected", ct);
            return RequestWithdrawalResult.Fail("No tienes saldo suficiente para ese retiro.");
        }

        return RequestWithdrawalResult.Ok(ToDto(record));
    }

    public async Task<IReadOnlyList<WithdrawalDto>> GetMyWithdrawalsAsync(string steamId64, CancellationToken ct = default)
    {
        var records = await _wallet.GetWithdrawalsAsync(steamId64, ct);
        return records.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<WithdrawalDto>> GetPendingWithdrawalsAsync(CancellationToken ct = default)
    {
        var records = await _wallet.GetPendingWithdrawalsAsync(ct);
        return records.Select(ToDto).ToList();
    }

    public async Task<bool> MarkWithdrawalPaidAsync(Guid id, CancellationToken ct = default)
    {
        var w = await _wallet.GetWithdrawalByIdAsync(id, ct);
        if (w is null || w.Status != "Pending") return false;

        await _wallet.UpdateWithdrawalStatusAsync(id, "Paid", ct);
        return true;
    }

    public async Task<bool> RejectWithdrawalAsync(Guid id, CancellationToken ct = default)
    {
        var w = await _wallet.GetWithdrawalByIdAsync(id, ct);
        if (w is null || w.Status != "Pending") return false;

        await _wallet.CreditAsync(w.SteamId64, w.Amount, "WithdrawalRefund", w.Id.ToString(), "Reembolso de retiro rechazado", ct);
        await _wallet.UpdateWithdrawalStatusAsync(id, "Rejected", ct);
        return true;
    }

    private static WithdrawalDto ToDto(WithdrawalRecord w) => new()
    {
        Id = w.Id,
        SteamId = w.SteamId64,
        Amount = w.Amount,
        Method = w.Method,
        Destination = w.Destination,
        Status = w.Status,
        CreatedAtUtc = w.CreatedAtUtc,
        ResolvedAtUtc = w.ResolvedAtUtc
    };
}
