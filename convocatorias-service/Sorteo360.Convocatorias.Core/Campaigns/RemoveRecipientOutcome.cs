namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// Los tres desenlaces de quitar un destinatario. Son tres y no un booleano porque "esa
/// campaña no existe" y "esa campaña existe y ese destinatario no" son dos 404 con dos
/// significados, y un booleano obliga a quien llama a volver a preguntar para distinguirlos.
/// </summary>
public enum RemoveRecipientOutcome
{
    /// <summary>No hay campaña con ese id.</summary>
    CampaignNotFound,

    /// <summary>La campaña existe y no tiene ese destinatario.</summary>
    RecipientNotFound,

    /// <summary>Se quitó.</summary>
    Removed
}
