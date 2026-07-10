using FluentValidation;
using SteamMarket.Api.Contracts;

namespace SteamMarket.Api.Validation;

public sealed class AddAdminAccountRequestValidator : AbstractValidator<AddAdminAccountRequest>
{
    public AddAdminAccountRequestValidator()
    {
        RuleFor(x => x.SteamId64)
            .NotEmpty().WithMessage("Falta el SteamID64.")
            .Matches(@"^\d{17}$").WithMessage("El SteamID64 debe ser un numero de 17 digitos.");

        RuleFor(x => x.TradeUrl)
            .NotEmpty().WithMessage("La Trade URL no puede estar vacia.")
            .Must(TradeUrlFormat.IsValid)
            .WithMessage("La Trade URL no tiene el formato de Steam esperado " +
                         "(https://steamcommunity.com/tradeoffer/new/?partner=...&token=...).");

        RuleFor(x => x.Label).MaximumLength(100);
    }
}
