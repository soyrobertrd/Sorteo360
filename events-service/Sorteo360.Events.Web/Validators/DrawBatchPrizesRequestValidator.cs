using FluentValidation;
using Sorteo360.Events.Web.RaffleEndpoints;

namespace Sorteo360.Events.Web.Validators;

/// <summary>
/// Valida el lote de sorteo: entre 1 y 10 premios distintos (decisión de producto A33).
/// </summary>
public sealed class DrawBatchPrizesRequestValidator : AbstractValidator<DrawBatchPrizesRequest>
{
    public const int MaxBatchSize = 10;

    public DrawBatchPrizesRequestValidator()
    {
        RuleFor(x => x.PrizeIds)
            .NotEmpty()
            .WithMessage("Debe indicar al menos un premio para sortear.")
            .Must(ids => ids is not null && ids.Count <= MaxBatchSize)
            .WithMessage($"Se pueden sortear como máximo {MaxBatchSize} premios por lote.")
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage("Los premios del lote no pueden repetirse.");
    }
}

