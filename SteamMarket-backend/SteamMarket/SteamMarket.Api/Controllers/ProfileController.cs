using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SteamMarket.Api.Contracts;
using SteamMarket.Application.Common.Interfaces;
using SteamMarket.Application.Services.Interfaces;

namespace SteamMarket.Api.Controllers;

[ApiController]
[Route("api/profile")]
public class ProfileController : SteamAuthControllerBase
{
    private readonly IProfileService _profile;
    private readonly IValidator<UpdateTradeUrlRequest> _tradeUrlValidator;
    private readonly IValidator<SavePersonalDataRequest> _personalDataValidator;

    public ProfileController(
        IProfileService profile,
        IValidator<UpdateTradeUrlRequest> tradeUrlValidator,
        IValidator<SavePersonalDataRequest> personalDataValidator)
    {
        _profile = profile;
        _tradeUrlValidator = tradeUrlValidator;
        _personalDataValidator = personalDataValidator;
    }

    /// <summary>Perfil del usuario logueado: nombre, SteamID y su Trade URL guardada (si tiene).</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (!TryGetSteamId(out var steamId64, out var unauthorized))
            return unauthorized!;

        var result = await _profile.GetProfileAsync(steamId64, User.Identity!.Name ?? "Jugador", ct);
        return Ok(result);
    }

    /// <summary>Guarda (o reemplaza) la Trade URL del usuario logueado.</summary>
    [HttpPut("trade-url")]
    public async Task<IActionResult> UpdateTradeUrl([FromBody] UpdateTradeUrlRequest request, CancellationToken ct)
    {
        if (!TryGetSteamId(out var steamId64, out var unauthorized))
            return unauthorized!;

        var validation = await _tradeUrlValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(
                validation.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())));
        }

        await _profile.UpdateTradeUrlAsync(steamId64, request.TradeUrl, ct);
        return Ok(new { ok = true, tradeUrl = request.TradeUrl });
    }

    /// <summary>Guarda (o reemplaza) los datos personales del usuario logueado.</summary>
    [HttpPut("personal-data")]
    public async Task<IActionResult> SavePersonalData([FromBody] SavePersonalDataRequest request, CancellationToken ct)
    {
        if (!TryGetSteamId(out var steamId64, out var unauthorized))
            return unauthorized!;

        var validation = await _personalDataValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(
                validation.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())));
        }

        await _profile.SavePersonalDataAsync(steamId64, new PersonalDataInput
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone,
            DocumentType = request.DocumentType,
            DocumentNumber = request.DocumentNumber
        }, ct);

        return Ok(new { ok = true });
    }

    /// <summary>Borra los datos personales del usuario logueado (conserva la Trade URL).</summary>
    [HttpDelete("personal-data")]
    public async Task<IActionResult> DeletePersonalData(CancellationToken ct)
    {
        if (!TryGetSteamId(out var steamId64, out var unauthorized))
            return unauthorized!;

        await _profile.DeletePersonalDataAsync(steamId64, ct);
        return Ok(new { ok = true });
    }
}
