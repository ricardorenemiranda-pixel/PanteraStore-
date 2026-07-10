using FluentValidation;
using SteamMarket.Api.Contracts;

namespace SteamMarket.Api.Validation;

/// <summary>
/// Un Trade URL de Steam tiene forma fija:
/// https://steamcommunity.com/tradeoffer/new/?partner=NUMERO&amp;token=TOKEN
/// Validamos el formato exacto para no guardar basura ni permitir que alguien meta un link
/// a otro dominio (ej. para phishing) en un campo que despues quizas se muestre o se use tal cual.
/// </summary>
public sealed class UpdateTradeUrlRequestValidator : AbstractValidator<UpdateTradeUrlRequest>
{
    public UpdateTradeUrlRequestValidator()
    {
        RuleFor(x => x.TradeUrl)
            .NotEmpty().WithMessage("La Trade URL no puede estar vacia.")
            .Must(TradeUrlFormat.IsValid)
            .WithMessage("La Trade URL no tiene el formato de Steam esperado " +
                         "(https://steamcommunity.com/tradeoffer/new/?partner=...&token=...).");
    }
}
