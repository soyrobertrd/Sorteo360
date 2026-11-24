using FluentValidation;
using Sorteo360.Events.Web.RaffleEndpoints;

namespace Sorteo360.Events.Web.Validators;

/// <summary>El motivo se mide ya recortado, y uno en blanco cuenta como ausente (spec 03 §3.3).</summary>
public sealed class ReversePrizeHandoverRequestValidator : AbstractValidator<ReversePrizeHandoverRequest>
{
    private const int ReasonMaxLength = 500;

    public ReversePrizeHandoverRequestValidator()
    {
        RuleFor(x => x.Reason)
            .Must(v => !string.IsNullOrWhiteSpace(v))
            .WithMessage("El motivo de la reversión es obligatorio.")
            .Must(v => v is null || v.Trim().Length <= ReasonMaxLength)
            .WithMessage($"El motivo de la reversión no puede superar {ReasonMaxLength} caracteres.");
    }
}
