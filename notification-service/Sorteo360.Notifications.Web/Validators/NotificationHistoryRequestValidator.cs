using FluentValidation;
using Sorteo360.Notifications.Core.Notifications;
using Sorteo360.Notifications.Web.NotificationEndpoints;

namespace Sorteo360.Notifications.Web.Validators;

/// <summary>
/// La forma de una consulta al historial. Hereda el techo del tamaño de página, que es lo que
/// contesta 400 en vez de traerse la tabla entera.
/// <para>
/// Los estados se validan contra el conjunto cerrado: un valor inventado no encontraría nada, y
/// una página vacía parece una respuesta. Vale más decir que el filtro no existe.
/// </para>
/// </summary>
public sealed class NotificationHistoryRequestValidator : PagingRequestValidator<NotificationHistoryRequest>
{
    /// <summary>Largo máximo de una dirección de correo según el RFC 5321.</summary>
    private const int MaxEmailLength = 320;

    /// <summary>
    /// Cuántos ids caben en una consulta: los de una página entera, ni uno más, así que una sola
    /// llamada los trae todos.
    /// </summary>
    public const int MaxIds = 100;

    public NotificationHistoryRequestValidator()
    {
        RuleFor(x => x.DeliveryStatus)
            .Must(status => DeliveryStatus.All.Contains(status!))
            .WithMessage($"El deliveryStatus debe ser uno de: {string.Join(", ", DeliveryStatus.All.Order(StringComparer.Ordinal))}.")
            .When(x => !string.IsNullOrWhiteSpace(x.DeliveryStatus));

        RuleFor(x => x.RecipientAction)
            .Must(action => RecipientAction.All.Contains(action!))
            .WithMessage($"El recipientAction debe ser uno de: {string.Join(", ", RecipientAction.All.Order(StringComparer.Ordinal))}.")
            .When(x => !string.IsNullOrWhiteSpace(x.RecipientAction));

        RuleFor(x => x.Email)
            .MaximumLength(MaxEmailLength)
            .WithMessage($"El email no puede pasar de {MaxEmailLength} caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        // Los dos o ninguno. Con uno solo el filtro se aplicaría a medias o no se aplicaría, y en
        // los dos casos la respuesta sería una lista que no es la que se pidió.
        RuleFor(x => x)
            .Must(x => string.IsNullOrWhiteSpace(x.EntityType) == string.IsNullOrWhiteSpace(x.EntityId))
            .WithMessage("Los parámetros 'entityType' y 'entityId' van juntos: o los dos o ninguno.");

        RuleFor(x => x.Ids)
            .Must(ids => ids!.Length <= MaxIds)
            .WithMessage($"No se pueden pedir más de {MaxIds} ids a la vez.")
            .When(x => x.Ids is not null);

        RuleFor(x => x.SentTo)
            .GreaterThanOrEqualTo(x => x.SentFrom!.Value)
            .WithMessage("sentTo debe ser mayor o igual que sentFrom.")
            .When(x => x.SentFrom is not null && x.SentTo is not null);
    }
}
