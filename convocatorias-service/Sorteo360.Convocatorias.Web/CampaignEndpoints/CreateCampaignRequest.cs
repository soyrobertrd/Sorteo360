namespace Sorteo360.Convocatorias.Web.CampaignEndpoints;

/// <summary>
/// Crear un borrador de campaña para un evento, o un mensaje directo sin evento: entonces la
/// plantilla es <c>direct_message</c> y el asunto y el texto vienen en el cuerpo (PGTI-174).
/// <para>
/// El idioma viaja en el cuerpo y no se lee del evento todavía: la lectura del evento vive en
/// el camino de lanzamiento (spec <c>04</c> §1), que es el paso siguiente de la cola, y es
/// donde se comprobará que el idioma de la campaña es el del evento. Como <c>language</c> se
/// congela al lanzar y hasta entonces es editable, el campo tiene que existir igualmente.
/// </para>
/// </summary>
public sealed record CreateCampaignRequest
{
    /// <summary>El evento que se convoca. Ausente en un mensaje directo.</summary>
    public Guid? EventId { get; init; }

    /// <summary>Qué correo sale. Una clave del catálogo de plantillas.</summary>
    public string? TemplateKey { get; init; }

    /// <summary>En qué idioma. Uno de los seis admitidos.</summary>
    public string? Language { get; init; }

    /// <summary>El asunto de un mensaje directo. Solo con <c>direct_message</c>.</summary>
    public string? Subject { get; init; }

    /// <summary>El texto plano de un mensaje directo. Solo con <c>direct_message</c>.</summary>
    public string? Message { get; init; }
}
