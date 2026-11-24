using Microsoft.AspNetCore.Mvc;
using Sorteo360.Notifications.Web.GlobalEndpoints;

namespace Sorteo360.Notifications.Web.NotificationEndpoints;

/// <summary>
/// Request enlazado desde la query string (con <c>[AsParameters]</c>) para el listado del
/// historial. Todos los filtros son opcionales; la paginación la hereda.
/// <para>
/// Los estados llegan como texto y no como enum: el binder de <c>[AsParameters]</c> respondería
/// 400 sin el código de máquina que este servicio le debe a quien consume, y el validador sí lo
/// pone.
/// </para>
/// </summary>
public record NotificationHistoryRequest : PagingRequest
{
    /// <summary>Tipo del envío, tal como lo declaró quien lo mandó.</summary>
    [FromQuery] public string? Type { get; init; }

    [FromQuery] public string? DeliveryStatus { get; init; }

    [FromQuery] public string? RecipientAction { get; init; }

    /// <summary>Dirección exacta. No busca por parecido ni pliega mayúsculas.</summary>
    [FromQuery] public string? Email { get; init; }

    /// <summary>Tipo de la entidad de la que trata el envío. Va siempre con <c>entityId</c>.</summary>
    [FromQuery] public string? EntityType { get; init; }

    /// <summary>Id de la entidad de la que trata el envío. Va siempre con <c>entityType</c>.</summary>
    [FromQuery] public string? EntityId { get; init; }

    [FromQuery] public string? SourceService { get; init; }

    /// <summary>
    /// Filas por su id, repitiendo el parámetro: <c>?id=a&amp;id=b</c>. Es el id que devolvió el
    /// <c>202</c> por persona, y con él quien mandó lee el estado de sus propios envíos de una vez.
    /// </summary>
    [FromQuery(Name = "id")] public Guid[]? Ids { get; init; }

    /// <summary>Desde cuándo, por la fecha en que la plataforma aceptó el envío.</summary>
    [FromQuery] public DateTimeOffset? SentFrom { get; init; }

    /// <summary>Hasta cuándo, por la fecha en que la plataforma aceptó el envío.</summary>
    [FromQuery] public DateTimeOffset? SentTo { get; init; }
}
