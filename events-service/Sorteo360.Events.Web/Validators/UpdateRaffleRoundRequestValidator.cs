using FluentValidation;
using Sorteo360.Events.Web.RaffleEndpoints;

namespace Sorteo360.Events.Web.Validators;

public sealed class UpdateRaffleRoundRequestValidator : AbstractValidator<UpdateRaffleRoundRequest>
{
    private const int NameMaxLength = 200;
    private const int DescriptionMaxLength = 1000;

    public UpdateRaffleRoundRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("El nombre de la ronda es obligatorio.")
            .MaximumLength(NameMaxLength)
            .WithMessage($"El nombre de la ronda no puede superar {NameMaxLength} caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(DescriptionMaxLength)
            .WithMessage($"La descripción de la ronda no puede superar {DescriptionMaxLength} caracteres.");
    }
}
