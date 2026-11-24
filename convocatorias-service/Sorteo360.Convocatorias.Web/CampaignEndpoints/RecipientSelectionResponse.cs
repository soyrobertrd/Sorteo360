namespace Sorteo360.Convocatorias.Web.CampaignEndpoints;

/// <summary>
/// Lo que dejó una selección. Las cuentas cuadran —<c>matched = added + excluded + skipped</c>—
/// porque es exactamente la pregunta de quien esperaba 240 y ve 227, y el desglose por motivo
/// contesta la otra mitad sin obligar a abrir la lista de exclusiones.
/// </summary>
public sealed record RecipientSelectionResponse
{
    /// <summary>Contactos que devolvió el CRM para ese filtro.</summary>
    public required int Matched { get; init; }

    /// <summary>Destinatarios nuevos que escribió esta selección.</summary>
    public required int Added { get; init; }

    /// <summary>Exclusiones nuevas que escribió esta selección.</summary>
    public required int Excluded { get; init; }

    /// <summary>Contactos que ya estaban en la campaña y no se volvieron a escribir.</summary>
    public required int Skipped { get; init; }

    /// <summary>
    /// Cuántas exclusiones nuevas por cada código: <c>no_email</c>, <c>opted_out</c>,
    /// <c>do_not_invite</c>, <c>contact_unavailable</c>. El cliente pinta su propio español por
    /// código.
    /// </summary>
    public required IReadOnlyDictionary<string, int> ExcludedByReason { get; init; }

    /// <summary>La campaña después de la selección, con sus cuentas ya al día.</summary>
    public required CampaignResponse Campaign { get; init; }
}
