using FluentValidation;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Web.EventEndpoints;

namespace Sorteo360.Events.Web.Validators;

public sealed class ListEventsRequestValidator : PagingRequestValidator<ListEventsRequest>
{
    public ListEventsRequestValidator()
    {
        RuleFor(x => x.Type)
            .Must(t => EventType.All.Contains(t!))
            .WithMessage($"El type/tipo de evento debe ser uno de: {string.Join(", ", EventType.All)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.Type));

        RuleFor(x => x.Status)
            .Must(s => EventStatus.All.Contains(s!))
            .WithMessage($"El status debe ser uno de: {string.Join(", ", EventStatus.All)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));

        RuleFor(x => x.Visibility)
            .Must(v => EventVisibility.All.Contains(v!))
            .WithMessage($"La visibilidad debe ser una de: {string.Join(", ", EventVisibility.All)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.Visibility));

        // 'active' y 'status' son mutuamente excluyentes: 'active' es un atajo sobre el status.
        RuleFor(x => x)
            .Must(x => x.Active is null || string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("Los parámetros 'active' y 'status' son mutuamente excluyentes.");

        // El término se compara con un ILIKE sin índice; un texto desmedido no encuentra más, solo
        // cuesta más.
        RuleFor(x => x.SearchTerm)
            .MaximumLength(200)
            .WithMessage("searchTerm no puede pasar de 200 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.SearchTerm));

        RuleFor(x => x.StartDateTo)
            .GreaterThanOrEqualTo(x => x.StartDateFrom!.Value)
            .WithMessage("startDateTo debe ser mayor o igual que startDateFrom.")
            .When(x => x.StartDateFrom is not null && x.StartDateTo is not null);
    }
}
