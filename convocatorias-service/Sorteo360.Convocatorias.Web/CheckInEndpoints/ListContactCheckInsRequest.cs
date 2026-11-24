using Microsoft.AspNetCore.Mvc;

namespace Sorteo360.Convocatorias.Web.CheckInEndpoints;

/// <summary>
/// Los contactos por los que se pregunta, repitiendo el parámetro:
/// <c>?contactId=a&amp;contactId=b</c>. Varios a la vez para que una página del listado de
/// contactos, o los contactos de una empresa, sean una llamada y no una por contacto.
/// </summary>
public sealed record ListContactCheckInsRequest
{
    [FromQuery(Name = "contactId")]
    public Guid[]? ContactIds { get; init; }
}
