using FluentValidation;
using Sorteo360.Events.Web.RaffleEndpoints;

namespace Sorteo360.Events.Web.Validators;

/// <summary>
/// Las seis autoridades son obligatorias y se miden ya recortadas, que es como se guardan: el acta
/// reemitida las nombra a todas, y un blanco nunca se rellena (spec 03 §8). Los topes son los del PATCH.
/// </summary>
public sealed class ReissueRaffleRequestValidator : AbstractValidator<ReissueRaffleRequest>
{
    private const int NameMaxLength = 200;
    private const int IdentityDocumentMaxLength = 100;

    public ReissueRaffleRequestValidator()
    {
        Required(RuleFor(x => x.NotaryName), "El nombre del notario", NameMaxLength);
        Required(RuleFor(x => x.NotaryIdentityDocument), "El documento del notario", IdentityDocumentMaxLength);
        Required(RuleFor(x => x.WitnessOneName), "El nombre del primer testigo", NameMaxLength);
        Required(RuleFor(x => x.WitnessOneIdentityDocument), "El documento del primer testigo", IdentityDocumentMaxLength);
        Required(RuleFor(x => x.WitnessTwoName), "El nombre del segundo testigo", NameMaxLength);
        Required(RuleFor(x => x.WitnessTwoIdentityDocument), "El documento del segundo testigo", IdentityDocumentMaxLength);
    }

    private static void Required(IRuleBuilderInitial<ReissueRaffleRequest, string?> rule, string field, int maxLength) =>
        rule
            .Must(v => !string.IsNullOrWhiteSpace(v))
            .WithMessage($"{field} es obligatorio.")
            .Must(v => v is null || v.Trim().Length <= maxLength)
            .WithMessage($"{field} no puede superar {maxLength} caracteres.");
}
