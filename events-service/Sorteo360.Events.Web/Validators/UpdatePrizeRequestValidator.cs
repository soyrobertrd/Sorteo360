using FluentValidation;
using Sorteo360.Events.Web.RaffleEndpoints;

namespace Sorteo360.Events.Web.Validators;

public sealed class UpdatePrizeRequestValidator : AbstractValidator<UpdatePrizeRequest>
{
    private const int NameMaxLength = 200;
    private const int DescriptionMaxLength = 1000;
    private const int ValueMaxLength = 100;

    public UpdatePrizeRequestValidator()
    {
        // Obligatoria a propósito (ADR-0009): omitirla no tiene a qué borrar, y adivinar que se
        // quería dejarlo donde estaba sería tratar este PATCH como parcial.
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

        RuleFor(x => x.SponsorContactId)
            .Null()
            .When(x => x.SponsorCompanyId is null)
            .WithMessage("El contacto del patrocinador solo puede indicarse junto con su empresa.");

        RuleFor(x => x.SponsorName)
            .MaximumLength(NameMaxLength)
            .WithMessage($"El nombre del patrocinador no puede superar {NameMaxLength} caracteres.");
    }
}
