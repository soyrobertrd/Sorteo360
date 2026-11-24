namespace Sorteo360.Notifications.Web.NotificationEndpoints;

/// <summary>
/// Los ids del envío aceptado, y nada más. No hay registro completo ni estado agregado: el id es el
/// hilo del que tirar si dentro de seis semanas alguien reporta un correo que no llegó.
/// </summary>
public record SendNotificationResponse
{
    /// <summary>Id del envío.</summary>
    public Guid BatchId { get; init; }

    /// <summary>Un resultado por persona, en el orden en que llegaron.</summary>
    public IReadOnlyList<RecipientResultResponse> Results { get; init; } = [];
}

/// <summary>
/// Qué pasó con una persona al aceptar el envío. <c>accepted</c> significa "lo guardamos y lo vamos
/// a intentar": no significa que el proveedor lo tomó, y nunca significa entregado.
/// </summary>
public record RecipientResultResponse
{
    /// <summary>Id de la fila de historial, o nulo si no se creó ninguna.</summary>
    public Guid? Id { get; init; }

    /// <summary>La dirección tal como llegó.</summary>
    public string Recipient { get; init; } = string.Empty;

    /// <summary><c>accepted</c> o <c>rejected</c>.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Código del rechazo, cuando hubo uno.</summary>
    public string? Reason { get; init; }
}
