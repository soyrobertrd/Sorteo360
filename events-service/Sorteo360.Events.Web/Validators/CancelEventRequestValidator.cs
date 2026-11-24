using FluentValidation;
using Sorteo360.Events.Web.EventEndpoints;

namespace Sorteo360.Events.Web.Validators;

public sealed class CancelEventRequestValidator : AbstractValidator<CancelEventRequest>
{
    public CancelEventRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("La razón de cancelación es obligatoria.");
    }
}
