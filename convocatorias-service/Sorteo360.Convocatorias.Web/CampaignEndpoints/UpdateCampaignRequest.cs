namespace Sorteo360.Convocatorias.Web.CampaignEndpoints;

/// <summary>
/// Editar un borrador. Solo las dos cosas que se congelan al lanzar: no se cambia de evento,
/// porque una campaña de otro evento es otra campaña.
/// <para>
/// Las dos son opcionales porque es un <c>PATCH</c>: lo que no viene no se toca.
/// </para>
/// </summary>
public sealed record UpdateCampaignRequest
{
    /// <summary>Nueva plantilla. Ausente deja la que tiene.</summary>
    public string? TemplateKey { get; init; }

    /// <summary>Nuevo idioma. Ausente deja el que tiene.</summary>
    public string? Language { get; init; }

    /// <summary>Nuevo asunto, solo de un mensaje directo. Ausente deja el que tiene.</summary>
    public string? Subject { get; init; }

    /// <summary>Nuevo texto, solo de un mensaje directo. Ausente deja el que tiene.</summary>
    public string? Message { get; init; }
}
