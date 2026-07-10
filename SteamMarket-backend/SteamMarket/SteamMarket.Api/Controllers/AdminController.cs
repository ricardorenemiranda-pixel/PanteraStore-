using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SteamMarket.Api.Contracts;
using SteamMarket.Application.Services.Interfaces;

namespace SteamMarket.Api.Controllers;

/// <summary>
/// Endpoints solo para administradores: aprobar ordenes de venta una vez que se recibio el
/// trade real en Steam, marcar retiros como pagados, y (solo el super admin) gestionar la
/// lista de cuentas admin adicionales.
///
/// "Admin" = el SteamId64 fijo de appsettings/user-secrets (Admin:SteamId64, el "super admin")
/// O cualquier cuenta agregada a la tabla AdminAccounts por ese super admin. Si la config esta
/// vacia, NADIE tiene acceso (default seguro) - hay que configurarla explicitamente.
/// </summary>
[ApiController]
[Route("api/admin")]
public class AdminController : SteamAuthControllerBase
{
    private readonly ISellOrderService _orders;
    private readonly IWalletService _wallet;
    private readonly IAdminAccountService _admins;
    private readonly IValidator<AddAdminAccountRequest> _addAdminValidator;

    public AdminController(
        ISellOrderService orders,
        IWalletService wallet,
        IAdminAccountService admins,
        IValidator<AddAdminAccountRequest> addAdminValidator)
    {
        _orders = orders;
        _wallet = wallet;
        _admins = admins;
        _addAdminValidator = addAdminValidator;
    }

    [HttpGet("orders/pending")]
    public async Task<IActionResult> GetPendingOrders(CancellationToken ct)
    {
        var (isAdmin, forbidden) = await TryGetAdminAsync(ct);
        if (!isAdmin) return forbidden!;
        return Ok(await _orders.GetPendingOrdersAsync(ct));
    }

    [HttpPost("orders/{id:guid}/complete")]
    public async Task<IActionResult> CompleteOrder(Guid id, CancellationToken ct)
    {
        var (isAdmin, forbidden) = await TryGetAdminAsync(ct);
        if (!isAdmin) return forbidden!;

        var ok = await _orders.CompleteOrderAsync(id, ct);
        if (!ok) return NotFound(new { error = "La orden no existe o ya fue resuelta." });

        return Ok(new { ok = true });
    }

    [HttpPost("orders/{id:guid}/reject")]
    public async Task<IActionResult> RejectOrder(Guid id, [FromBody] RejectOrderRequest? request, CancellationToken ct)
    {
        var (isAdmin, forbidden) = await TryGetAdminAsync(ct);
        if (!isAdmin) return forbidden!;

        var ok = await _orders.RejectOrderAsync(id, request?.Note, ct);
        if (!ok) return NotFound(new { error = "La orden no existe o ya fue resuelta." });

        return Ok(new { ok = true });
    }

    [HttpGet("withdrawals/pending")]
    public async Task<IActionResult> GetPendingWithdrawals(CancellationToken ct)
    {
        var (isAdmin, forbidden) = await TryGetAdminAsync(ct);
        if (!isAdmin) return forbidden!;
        return Ok(await _wallet.GetPendingWithdrawalsAsync(ct));
    }

    [HttpPost("withdrawals/{id:guid}/paid")]
    public async Task<IActionResult> MarkWithdrawalPaid(Guid id, CancellationToken ct)
    {
        var (isAdmin, forbidden) = await TryGetAdminAsync(ct);
        if (!isAdmin) return forbidden!;

        var ok = await _wallet.MarkWithdrawalPaidAsync(id, ct);
        if (!ok) return NotFound(new { error = "El retiro no existe o ya fue resuelto." });

        return Ok(new { ok = true });
    }

    [HttpPost("withdrawals/{id:guid}/reject")]
    public async Task<IActionResult> RejectWithdrawal(Guid id, CancellationToken ct)
    {
        var (isAdmin, forbidden) = await TryGetAdminAsync(ct);
        if (!isAdmin) return forbidden!;

        var ok = await _wallet.RejectWithdrawalAsync(id, ct);
        if (!ok) return NotFound(new { error = "El retiro no existe o ya fue resuelto." });

        return Ok(new { ok = true });
    }

    // --- Gestion de cuentas admin (solo el super admin de config) ---

    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts(CancellationToken ct)
    {
        if (!TryGetSuperAdmin(out var forbidden)) return forbidden!;
        return Ok(await _admins.GetAllAsync(ct));
    }

    [HttpPost("accounts")]
    public async Task<IActionResult> AddAccount([FromBody] AddAdminAccountRequest request, CancellationToken ct)
    {
        if (!TryGetSuperAdmin(out var forbidden)) return forbidden!;

        var validation = await _addAdminValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(
                validation.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())));
        }

        var error = await _admins.AddOrUpdateAsync(request.SteamId64, request.TradeUrl, request.Label, ct);
        if (error is not null) return BadRequest(new { error });

        return Ok(new { ok = true });
    }

    [HttpDelete("accounts/{steamId64}")]
    public async Task<IActionResult> RemoveAccount(string steamId64, CancellationToken ct)
    {
        if (!TryGetSuperAdmin(out var forbidden)) return forbidden!;

        var error = await _admins.RemoveAsync(steamId64, ct);
        if (error is not null) return BadRequest(new { error });

        return Ok(new { ok = true });
    }

    // Los metodos async no pueden tener parametros "out" (CS1988), asi que devolvemos
    // una tupla en vez del patron TryXxx(out ...) que si usa TryGetSuperAdmin (ese es sincrono).
    private async Task<(bool isAdmin, IActionResult? forbidden)> TryGetAdminAsync(CancellationToken ct)
    {
        if (!TryGetSteamId(out var steamId64, out var unauthorized))
            return (false, unauthorized);

        if (!await _admins.IsAdminAsync(steamId64, ct))
            return (false, Forbidden());

        return (true, null);
    }

    private bool TryGetSuperAdmin(out IActionResult? forbidden)
    {
        if (!TryGetSteamId(out var steamId64, out var unauthorized))
        {
            forbidden = unauthorized;
            return false;
        }

        if (!_admins.IsSuperAdmin(steamId64))
        {
            forbidden = Forbidden();
            return false;
        }

        forbidden = null;
        return true;
    }

    // OJO: no usar Forbid() (ver comentario original): con auth por cookie, ForbidResult dispara
    // el pipeline de autenticacion que por default intenta REDIRIGIR en vez de devolver JSON
    // llano, rompiendo el contrato de la API. StatusCode() evita ese pipeline.
    private IActionResult Forbidden() =>
        StatusCode(StatusCodes.Status403Forbidden, new { error = "No tienes acceso de administrador." });
}
