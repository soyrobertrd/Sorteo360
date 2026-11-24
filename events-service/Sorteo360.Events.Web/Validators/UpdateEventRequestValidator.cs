using FluentValidation;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Web.EventEndpoints;

namespace Sorteo360.Events.Web.Validators;

public sealed class UpdateEventRequestValidator : AbstractValidator<UpdateEventRequest>
{
    public UpdateEventRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();

        RuleFor(x => x.StartDate).NotEqual(default(DateOnly));

        RuleFor(x => x.EndDate)
            .NotEqual(default(DateOnly))
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("La fecha de fin debe ser igual o posterior a la fecha de inicio.");

        RuleFor(x => x.Language)
            .Must(l => AllowedLanguage.All.Contains(l!))
            .WithMessage($"El idioma debe ser uno de: {string.Join(", ", AllowedLanguage.All)}.")
            .When(x => x.Language is not null);

        RuleFor(x => x.Visibility)
            .Must(v => EventVisibility.All.Contains(v!))
            .WithMessage($"La visibilidad debe ser una de: {string.Join(", ", EventVisibility.All)}.")
            .When(x => x.Visibility is not null);

        RuleFor(x => x.SeatingLayout)
            .Must(l => SeatingLayout.All.Contains(l!))
            .WithMessage($"El montaje del salón debe ser uno de: {string.Join(", ", SeatingLayout.All)}.")
            .When(x => x.SeatingLayout is not null);

        RuleFor(x => x.ExternalUrl)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("El link del roadshow debe ser una URL válida.")
            .When(x => x.ExternalUrl is not null);

        RuleFor(x => x.InvitedCount)
            .GreaterThan(0)
            .When(x => x.InvitedCount is not null);
    }
}
