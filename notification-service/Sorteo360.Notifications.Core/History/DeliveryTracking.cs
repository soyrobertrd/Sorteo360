using Sorteo360.Notifications.Core.Notifications;

namespace Sorteo360.Notifications.Core.History;

/// <summary>
/// El tope que el historial le pone al estado de una fila cuando nadie está siguiendo la entrega.
/// <para>
/// <b>Normativo: con <c>tracking_expected</c> en falso, el estado de entrega nunca pasa de
/// <c>sent</c>.</b> Sin esta regla, una pantalla leería un <c>sent</c> permanente como una cola
/// atascada y saldría a buscar un problema que no existe; y un evento que llegara por cualquier
/// otra vía movería una fila de un envío que se aceptó sabiendo que no habría seguimiento.
/// </para>
/// <para>
/// El tope se aplica al leer y no al escribir: la columna guarda lo que pasó, y lo que se reporta
/// es lo que el envío podía saber. Así una fila escrita sin webhook sigue diciendo la verdad si
/// después se enciende uno.
/// </para>
/// </summary>
public static class DeliveryTracking
{
    /// <summary>
    /// Los estados que solo escribe el webhook del proveedor. Son exactamente los que el tope
    /// devuelve a <c>sent</c>: <c>dropped</c> y <c>failed</c> no están porque los escribe este
    /// servicio y no son un paso más allá de <c>sent</c>, son otro final.
    /// </summary>
    public static readonly IReadOnlySet<string> ProviderDerived = new HashSet<string>(StringComparer.Ordinal)
    {
        DeliveryStatus.Delivered,
        DeliveryStatus.Bounced,
        DeliveryStatus.Deferred
    };

    /// <summary>
    /// El estado que se reporta para una fila, dado lo guardado y si su envío esperaba
    /// seguimiento.
    /// </summary>
    /// <param name="storedStatus">El estado tal como está en la columna.</param>
    /// <param name="trackingExpected">Si el envío se aceptó con un webhook configurado.</param>
    public static string Reported(string storedStatus, bool trackingExpected) =>
        !trackingExpected && ProviderDerived.Contains(storedStatus)
            ? DeliveryStatus.Sent
            : storedStatus;
}
