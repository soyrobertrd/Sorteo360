using Microsoft.AspNetCore.Mvc;

namespace Sorteo360.Convocatorias.Web.ContactCampaignEndpoints;

/// <summary>
/// Los contactos por los que se pregunta, repitiendo el parámetro:
/// <c>?contactId=a&amp;contactId=b</c>. Los de una empresa son una llamada y no una por contacto.
/// </summary>
public sealed record ListContactCampaignsRequest
{
    [FromQuery(Name = "contactId")]
    public Guid[]? ContactIds { get; init; }
}
