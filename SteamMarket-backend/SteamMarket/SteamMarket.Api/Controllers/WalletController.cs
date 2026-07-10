using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SteamMarket.Api.Contracts;
using SteamMarket.Application.Services.Interfaces;

namespace SteamMarket.Api.Controllers;

[ApiController]
[Route("api/wallet")]
public class WalletController : SteamAuthControllerBase
{
    private readonly IWalletService _wallet;
    private readonly IValidator<RequestWithdrawalRequest> _withdrawalValidator;

    public WalletController(IWalletService wallet, IValidator<RequestWithdrawalRequest> withdrawalValidator)
    {
        _wallet = wallet;
        _withdrawalValidator = withdrawalValidator;
    }

    /// <summary>Saldo actual e historial de movimientos del usuario logueado.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (!TryGetSteamId(out var steamId64, out var unauthorized))
            return unauthorized!;

        var wallet = await _wallet.GetWalletAsync(steamId64, ct);
        return Ok(wallet);
    }

    /// <summary>Pide un retiro (Yape/Transferencia). Debita el saldo al toque; si no alcanza, falla.</summary>
    [HttpPost("withdrawals")]
    public async Task<IActionResult> RequestWithdrawal([FromBody] RequestWithdrawalRequest request, CancellationToken ct)
    {
        if (!TryGetSteamId(out var steamId64, out var unauthorized))
            return unauthorized!;

        var validation = await _withdrawalValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(
                validation.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())));
        }

        var result = await _wallet.RequestWithdrawalAsync(steamId64, request.Amount, request.Method, request.Destination, ct);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Ok(new { success = true, withdrawal = result.Withdrawal });
    }

    /// <summary>Historial de retiros pedidos por el usuario logueado.</summary>
    [HttpGet("withdrawals")]
    public async Task<IActionResult> GetMyWithdrawals(CancellationToken ct)
    {
        if (!TryGetSteamId(out var steamId64, out var unauthorized))
            return unauthorized!;

        var withdrawals = await _wallet.GetMyWithdrawalsAsync(steamId64, ct);
        return Ok(withdrawals);
    }
}
