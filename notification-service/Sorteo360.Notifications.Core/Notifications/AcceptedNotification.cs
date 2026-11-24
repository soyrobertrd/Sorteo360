namespace Sorteo360.Notifications.Core.Notifications;

/// <summary>
/// Lo que se le contesta a quien llama: los ids y nada más. No hay registro completo ni estado
/// agregado, porque un estado que diría "pendiente" siempre es una invitación a ramificar, y no
/// ramificar es justo el punto de mandar y olvidarse.
/// </summary>
/// <param name="BatchId">Id del envío.</param>
/// <param name="Results">Un resultado por persona, en el orden en que llegaron.</param>
public sealed record AcceptedNotification(Guid BatchId, IReadOnlyList<RecipientOutcome> Results);

/// <summary>
/// Qué pasó con una persona en el momento de aceptar. <c>accepted</c> significa "lo guardamos y
/// lo vamos a intentar": no significa que el proveedor lo tomó, y nunca significa entregado.
/// </summary>
/// <param name="Id">Id de la fila de historial, o null si no se creó ninguna.</param>
/// <param name="Recipient">La dirección tal como llegó.</param>
/// <param name="Status">
/// <c>accepted</c> o <c>rejected</c>. Lo único que se puede rechazar acá es una dirección que no
/// es una dirección; una dirección suprimida se acepta y la marca el worker.
/// </param>
/// <param name="Reason">Código del rechazo, cuando hubo uno.</param>
public sealed record RecipientOutcome(Guid? Id, string Recipient, string Status, string? Reason)
{
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";

    /// <summary>Motivo del único rechazo posible en el momento de aceptar.</summary>
    public const string AddressInvalid = "RECIPIENT_ADDRESS_INVALID";

    public static RecipientOutcome ForAccepted(Guid id, string recipient) =>
        new(id, recipient, Accepted, null);

    public static RecipientOutcome ForInvalidAddress(string recipient) =>
        new(null, recipient, Rejected, AddressInvalid);
}
