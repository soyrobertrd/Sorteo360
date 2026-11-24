namespace Sorteo360.Convocatorias.Core.Events;

/// <summary>
/// Un evento visto desde convocatorias: lo poco que este servicio necesita de él, y nada más.
/// <para>
/// events-service devuelve bastante más —presupuesto, costo total, invitados previstos, el
/// contacto de la OPT— y ese es el costo aceptado de leer sus endpoints tal como están en vez de
/// pedirle una proyección propia (spec <c>01</c> §4). Lo que no entra acá no puede salir por el
/// portal por accidente.
/// </para>
/// <para>
/// <b>Nada de esto se guarda.</b> El evento es de events-service; acá se lee al lanzar y al
/// servir el portal, y se descarta.
/// </para>
/// </summary>
public sealed record EventSummary
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Status { get; init; }
    public required string Language { get; init; }
    public required string Visibility { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public string? ExternalUrl { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<EventCity> Cities { get; init; } = [];

    /// <summary>
    /// El nombre de la OPT que organiza, resuelto por events-service contra el CRM.
    /// <para>
    /// Nulo no es un evento sin organizador: es el directorio del CRM caído cuando se armó esa
    /// respuesta (ADR-0013). El detalle público omite el bloque de organizador en vez de dibujar
    /// un hueco, y sigue sirviendo.
    /// </para>
    /// </summary>
    public string? OptName { get; init; }

    /// <summary>Si se publica. Lo pregunta el portal antes de servir el detalle por id.</summary>
    public bool IsPublic =>
        Visibility.Equals(EventVisibility.Public, StringComparison.OrdinalIgnoreCase);

    /// <summary>Si todavía no terminó.</summary>
    public bool IsActive => EventStatus.Active.Contains(Status);

    /// <summary>Si fue cancelado. Bloquea el lanzamiento y es 404 en todo el portal.</summary>
    public bool IsCancelled =>
        Status.Equals(EventStatus.Cancelled, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Si todavía se puede uno inscribir. <b>Nada que ver con la visibilidad</b>: a un evento
    /// privado se inscribe quien recibió su invitación, así que lo único que cierra la puerta
    /// acá es el estado y la fecha (spec <c>06</c> §2.1 y §4.6).
    /// <para>
    /// <b>Son dos condiciones y no una</b>, a propósito. El estado por su cuenta dejaría entrar
    /// a un evento vivo cuya última fecha ya pasó; la fecha por su cuenta dejaría entrar a uno
    /// cerrado antes de tiempo, que es exactamente lo que §4.6 dice que no hay que hacer. La
    /// cancelación entra por el estado: <c>cancelled</c> no está entre los activos.
    /// </para>
    /// <para>
    /// La ventana de inscripción de §5 es otra cosa y no está acá: es tabla de este servicio,
    /// llega con PGTI-61, y se niega con su propio código porque el evento sigue en pie.
    /// </para>
    /// </summary>
    /// <param name="today">
    /// El día de hoy. Se pasa en vez de leerse de un reloj para que esto se pueda probar, y va
    /// en UTC, como toda fecha del servicio.
    /// </param>
    public bool AcceptsRegistrationsOn(DateOnly today) => IsActive && EndDate >= today;
}
