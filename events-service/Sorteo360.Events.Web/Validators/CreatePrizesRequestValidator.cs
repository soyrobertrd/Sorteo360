using FluentValidation;
using Sorteo360.Events.Web.RaffleEndpoints;

namespace Sorteo360.Events.Web.Validators;

public sealed class CreatePrizesRequestValidator : AbstractValidator<CreatePrizesRequest>
{
    private const int NameMaxLength = 200;
    private const int DescriptionMaxLength = 1000;
    private const int ValueMaxLength = 100;

    // Un error de tipeo no puede crear quinientas filas; si hacen falta más, es un segundo alta.
    private const int MinQuantity = 1;
    private const int MaxQuantity = 50;

    public CreatePrizesRequestValidator()
    {
        RuleFor(x => x.RoundId)
            .NotEmpty()
            .WithMessage("La ronda del premio es obligatoria.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("El nombre del premio es obligatorio.")
            .MaximumLength(NameMaxLength)
            .WithMessage($"El nombre del premio no puede superar {NameMaxLength} caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(DescriptionMaxLength)
            .WithMessage($"La descripción del premio no puede superar {DescriptionMaxLength} caracteres.");

        RuleFor(x => x.Value)
            .MaximumLength(ValueMaxLength)
            .WithMessage($"El valor del premio no puede superar {ValueMaxLength} caracteres.");

        RuleFor(x => x.Quantity)
            .NotNull()
            .WithMessage("La cantidad de premios es obligatoria.")
            .InclusiveBetween(MinQuantity, MaxQuantity)
            .WithMessage($"La cantidad de premios debe estar entre {MinQuantity} y {MaxQuantity}.");

        RuleFor(x => x.SponsorContactId)
            .Null()
            .When(x => x.SponsorCompanyId is null)
            .WithMessage("El contacto del patrocinador solo puede indicarse junto con su empresa.");

        RuleFor(x => x.SponsorName)
            .MaximumLength(NameMaxLength)
            .WithMessage($"El nombre del patrocinador no puede superar {NameMaxLength} caracteres.");
    }
}
