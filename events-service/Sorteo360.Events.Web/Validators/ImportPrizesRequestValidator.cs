using FluentValidation;
using Sorteo360.Events.Web.RaffleEndpoints;

namespace Sorteo360.Events.Web.Validators;

public sealed class ImportPrizesRequestValidator : AbstractValidator<ImportPrizesRequest>
{
    public const int MaxPrizesPerImport = 10_000;

    public ImportPrizesRequestValidator()
    {
        RuleFor(x => x.Prizes)
            .NotNull().WithMessage("La lista de premios es obligatoria.")
            .NotEmpty().WithMessage("Debe incluir al menos un premio para importar.")
            .Must(items => items is null || ComputeTotalQuantity(items) is >= 1 and <= MaxPrizesPerImport)
            .WithMessage($"La importación debe contener entre 1 y {MaxPrizesPerImport:N0} premios en total.");

        RuleForEach(x => x.Prizes).ChildRules(item =>
        {
            item.RuleFor(p => p.RoundNumber)
                .NotNull().WithMessage("El número de ronda es obligatorio.")
                .GreaterThanOrEqualTo(1).WithMessage("El número de ronda debe ser mayor o igual a 1.");

            item.RuleFor(p => p.Name)
                .NotEmpty().WithMessage("El nombre del premio es obligatorio.")
                .MaximumLength(200)
                .WithMessage("El nombre del premio no puede superar 200 caracteres.");

            item.RuleFor(p => p.Value)
                .MaximumLength(100)
                .WithMessage("El valor del premio no puede superar 100 caracteres.")
                .When(p => !string.IsNullOrWhiteSpace(p.Value));

            item.RuleFor(p => p.SponsorName)
                .MaximumLength(200)
                .WithMessage("El nombre del patrocinador no puede superar 200 caracteres.")
                .When(p => !string.IsNullOrWhiteSpace(p.SponsorName));

            item.RuleFor(p => p.Quantity)
                .NotNull().WithMessage("La cantidad es obligatoria.")
                .GreaterThanOrEqualTo(1).WithMessage("La cantidad debe ser mayor o igual a 1.");
        });
    }

    private static long ComputeTotalQuantity(IReadOnlyList<ImportPrizeItemRequest> items)
    {
        long total = 0;
        foreach (var item in items)
        {
            if (item?.Quantity is > 0)
                total += item.Quantity.Value;
        }
        return total;
    }
}

