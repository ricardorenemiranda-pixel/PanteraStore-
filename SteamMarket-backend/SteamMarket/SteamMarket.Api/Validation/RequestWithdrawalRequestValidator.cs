using FluentValidation;
using SteamMarket.Api.Contracts;

namespace SteamMarket.Api.Validation;

public sealed class RequestWithdrawalRequestValidator : AbstractValidator<RequestWithdrawalRequest>
{
    private static readonly string[] AllowedMethods = { "Yape", "Transferencia" };

    public RequestWithdrawalRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("El monto debe ser mayor a 0.");

        RuleFor(x => x.Method)
            .Must(m => AllowedMethods.Contains(m))
            .WithMessage($"Metodo invalido. Valores permitidos: {string.Join(", ", AllowedMethods)}.");

        RuleFor(x => x.Destination)
            .NotEmpty().WithMessage("Falta el numero de Yape o la cuenta de destino.")
            .MaximumLength(100);
    }
}
