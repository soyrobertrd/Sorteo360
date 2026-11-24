using CSharpFunctionalExtensions;
using Sorteo360.Notifications.Core.DTOs;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Notifications.Core.Notifications;

/// <summary>
/// Aceptar un envío: comprobarlo, guardarlo y devolver los ids. Nada de esto manda correo; eso lo
/// hace el worker después, fuera de la petición.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Comprueba la petición y la guarda. Todo lo que puede rechazarla se pregunta antes de
    /// escribir la primera fila, en este orden: el tope de personas, las claves repetidas entre los
    /// datos comunes y los de una persona, y los marcadores sin valor. Una petición rechazada no
    /// deja rastro.
    /// <para>
    /// Una clave de idempotencia repetida con el mismo contenido no crea nada y devuelve los
    /// mismos ids; con otro contenido es un conflicto.
    /// </para>
    /// </summary>
    /// <param name="dto">El envío pedido.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<Result<AcceptedNotification, BaseError>> AcceptAsync(
        SendNotificationDto dto,
        CancellationToken cancellationToken);
}
