using FluentValidation;
using SteamMarket.Api.Contracts;

namespace SteamMarket.Api.Validation;

/// <summary>
/// Todos los campos de datos personales son opcionales (el usuario puede guardar solo algunos),
/// pero si vienen, deben tener formato valido.
/// </summary>
public sealed class SavePersonalDataRequestValidator : AbstractValidator<SavePersonalDataRequest>
{
    private static readonly string[] AllowedDocumentTypes = { "DNI", "CE", "Pasaporte" };

    public SavePersonalDataRequestValidator()
    {
        RuleFor(x => x.FirstName).MaximumLength(100);
        RuleFor(x => x.LastName).MaximumLength(100);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.DocumentNumber).MaximumLength(20);

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("El email no tiene un formato valido.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.DocumentType)
            .Must(type => AllowedDocumentTypes.Contains(type))
            .WithMessage($"Tipo de documento invalido. Valores permitidos: {string.Join(", ", AllowedDocumentTypes)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.DocumentType));
    }
}
