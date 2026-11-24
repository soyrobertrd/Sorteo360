namespace Sorteo360.Convocatorias.Web.CampaignEndpoints;

/// <summary>
/// El filtro con el que se construye la lista desde el CRM. Las dos dimensiones son repetibles,
/// <b>O dentro de cada una y Y entre ellas</b>, y la regla la aplica el CRM (spec <c>03</c> §6).
/// <para>
/// Sin ninguna de las dos el filtro trae el directorio entero, que es una convocatoria masiva
/// legítima. Lo que impide mandarla sin querer no es este cuerpo sino el
/// <c>expectedRecipientCount</c> del lanzamiento: la cuenta se ve antes de apretar.
/// </para>
/// <para>
/// <c>contactIds</c> es la otra forma de elegir (PGTI-179): los contactos que alguien marcó a mano
/// en el CRM. Va sola, sin filtro al lado, y no vacía.
/// </para>
/// <para>
/// <c>contactListId</c> es la tercera (PGTI-173): una lista de contactos de la OPT de quien
/// llama. También va sola.
/// </para>
/// </summary>
public sealed record SelectRecipientsRequest
{
    /// <summary>Clasificaciones de contacto, por código.</summary>
    public IReadOnlyList<string>? Classifications { get; init; }

    /// <summary>Categorías de empresa, por código.</summary>
    public IReadOnlyList<string>? CompanyCategories { get; init; }

    /// <summary>Contactos elegidos, por id. Excluye los dos filtros.</summary>
    public IReadOnlyList<Guid>? ContactIds { get; init; }

    /// <summary>Una lista de contactos del CRM, por id. Excluye los filtros y los ids.</summary>
    public Guid? ContactListId { get; init; }
}
