using FluentValidation;
using Sorteo360.Events.Web.EventEndpoints;

namespace Sorteo360.Events.Web.Validators;

public sealed class MoneyRequestValidator : AbstractValidator<MoneyRequest>
{
    public MoneyRequestValidator()
    {
        // Las columnas de montos son numeric(18,4): sin este límite, un quinto decimal se
        // redondearía en silencio al guardar. Los ceros finales no cuentan.
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .PrecisionScale(18, 4, true);

        RuleFor(x => x.Currency)
            .Must(c => c is "USD" or "EUR")
            .WithMessage("La moneda debe ser USD o EUR.");
    }
}
