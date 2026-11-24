using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Sorteo360.Events.Core.CompanyDirectory;
using Sorteo360.Events.Core.CrmDirectory;

namespace Sorteo360.Events.Infrastructure.Crm;

/// <summary>
/// Los contactos de una empresa, siempre en vivo contra el CRM.
/// <para>
/// No hay caché acá y eso es la decisión, no una carencia. La copia del directorio existe porque un
/// listado dibuja cientos de filas de una sola consulta; los contactos los pide una sola pantalla,
/// de una sola empresa, y son justamente el dato por el que alguien levanta el teléfono en medio de
/// un evento: servir un número de hace una semana sería peor que decir que no se sabe (ADR-0015).
/// </para>
/// <para>
/// Como no hay copia, tampoco hay escalón intermedio: o se resuelve, o no. Lo que sí se comparte con
/// los dos directorios es <see cref="CrmDirectoryStatusContext"/>, para que el cliente siga leyendo
/// una sola cabecera y dibuje un solo aviso.
/// </para>
/// </summary>
public sealed class CrmCompanyContactDirectory : ICompanyContactDirectory
{
    private readonly ICrmCompanyClient _client;
    private readonly CrmDirectoryStatusContext _statusContext;
    private readonly ILogger<CrmCompanyContactDirectory> _logger;

    public CrmCompanyContactDirectory(
        ICrmCompanyClient client,
        CrmDirectoryStatusContext statusContext,
        ILogger<CrmCompanyContactDirectory> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(statusContext);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _statusContext = statusContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Maybe<IReadOnlyList<CompanyContact>>> GetContactsAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        try
        {
            var page = await _client.GetCompanyContactsAsync(companyId, cancellationToken);

            // Un 404 sale como lista vacía y no como None, porque None significa "no se pudo
            // averiguar" y acá el CRM sí contestó. Hoy no llega ninguno —su lectura de contactos no
            // comprueba que la empresa exista, así que un id desconocido devuelve la página vacía—,
            // pero la ruta lo declara y el día que lo emita no puede confundirse con una avería. La
            // ficha se lee igual en los dos casos: un proveedor cuya empresa alguien borró sigue
            // mostrando lo que este servicio sí posee.
            // El `?? []` cubre las dos formas de "el CRM contestó sin contactos": el 404 de arriba
            // y un cuerpo vacío o con `items` nulo. Un NRE acá no sería HttpRequestException, así
            // que se escaparía del catch y tumbaría la ficha entera — justo lo que esta firma
            // promete que no pasa.
            var contacts = (page?.Items ?? []).Select(ToContact).ToList();

            // Un acierto no informa nada, igual que CachedCompanyDirectory.GetCompanyAsync: la
            // ficha resuelve su empresa antes de llegar acá y eso ya puso la cabecera, y el contexto
            // se queda con el peor estado, así que un "ok" nunca cambiaría nada. Solo se informa el
            // fallo, más abajo, porque ese sí es un dato que nadie más lleva.
            return Maybe<IReadOnlyList<CompanyContact>>.From(contacts);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "No se pudieron consultar los contactos de la empresa {CompanyId} en el CRM; la ficha va sin ellos.",
                companyId);

            // Degrada la respuesta entera, igual que un relleno del directorio que no se pudo hacer:
            // lo que se sirve es real, pero no está todo, y el cliente dibuja su aviso a partir de
            // esta cabecera y no de inspeccionar campo por campo.
            _statusContext.Report(CrmDirectoryStatus.Stale);

            return Maybe<IReadOnlyList<CompanyContact>>.None;
        }
    }

    private static CompanyContact ToContact(CrmCompanyContactResponse contact) =>
        new(contact.ContactId, contact.Name, contact.Email, contact.Phone, contact.JobTitle);
}
