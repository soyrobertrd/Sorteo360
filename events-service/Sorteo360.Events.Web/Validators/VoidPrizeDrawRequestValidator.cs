using FluentValidation;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Web.RaffleEndpoints;

namespace Sorteo360.Events.Web.Validators;

/// <summary>
/// La nota se mide ya recortada, que es como se guarda, y una en blanco cuenta como ausente: con
/// <c>other</c> es la única explicación que va a tener el acta (spec 02 §3).
/// </summary>
public sealed class VoidPrizeDrawRequestValidator : AbstractValidator<VoidPrizeDrawRequest>
{
    private const int NoteMaxLength = 500;

    public VoidPrizeDrawRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("El motivo de la anulación es obligatorio.")
            .Must(c => c is null || PrizeDrawVoidCode.All.Contains(c))
            .WithMessage($"El motivo de la anulación debe ser uno de: {string.Join(", ", PrizeDrawVoidCode.All)}.");

        RuleFor(x => x.Note)
            .Must(n => n is null || n.Trim().Length <= NoteMaxLength)
            .WithMessage($"La nota no puede superar {NoteMaxLength} caracteres.");

        RuleFor(x => x.Note)
            .Must(n => !string.IsNullOrWhiteSpace(n))
            .When(x => x.Code == PrizeDrawVoidCode.Other)
            .WithMessage("La nota es obligatoria cuando el motivo es other.");
    }
}
