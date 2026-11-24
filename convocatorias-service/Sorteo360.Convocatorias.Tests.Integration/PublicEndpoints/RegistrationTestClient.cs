using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Portal;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;

namespace Sorteo360.Convocatorias.Tests.Integration.PublicEndpoints;

/// <summary>
/// Lo que repiten los tests del portal: una invitación ya mandada, y las dos llamadas del
/// desconocido que la abre.
/// <para>
/// <b>La invitación se siembra por SQL y no lanzando una campaña.</b> Lo que hace falta acá es
/// un <c>campaign_recipient</c> con un hash de token, y llegar a eso por la API pediría crear,
/// seleccionar, lanzar y esperar al worker — cuatro pasos de otra spec cuyos fallos aparecerían
/// como fallos de este portal. Que la entrega escriba ese hash ya lo cobra
/// <c>CampaignHandoffTests</c>.
/// </para>
/// <para>
/// El texto plano del token lo acuña el test y no sale de ninguna lectura: la base guarda solo
/// el hash, así que no hay de dónde sacarlo después (spec <c>06</c> §2.1).
/// </para>
/// </summary>
internal static class RegistrationTestClient
{
    public const string ResolveRoute = "/v1/public/registrations/resolve";
    public const string RegisterRoute = "/v1/public/registrations";

    /// <summary>La página de la entrada de un <c>qrCode</c>: el código va en la ruta (spec <c>06</c> §2.3).</summary>
    public static Task<HttpResponseMessage> TicketAsync(HttpClient client, string qrCode) =>
        client.GetAsync($"{RegisterRoute}/{qrCode}");

    /// <summary>La imagen PNG del QR de un <c>qrCode</c>.</summary>
    public static Task<HttpResponseMessage> QrAsync(HttpClient client, string qrCode) =>
        client.GetAsync($"{RegisterRoute}/{qrCode}/qr");

    /// <summary>
    /// Deja mandada una invitación para ese evento y devuelve el token en claro, que es lo que
    /// llevaría el enlace del correo.
    /// </summary>
    public static async Task<SeededInvitation> InviteAsync(
        ConvocatoriasApiDbFactory factory,
        Guid eventId,
        string email = "invitada@acme.com",
        string firstName = "Ana",
        string lastName = "Reyes")
    {
        var token = PortalToken.Mint();
        var campaignId = Guid.CreateVersion7();
        var recipientId = Guid.CreateVersion7();
        var actor = Guid.CreateVersion7();

        await ExecuteAsync(factory, $"""
            INSERT INTO convocatorias.campaign
                (id, event_id, template_key, language, state,
                 created_at, updated_at, created_by_id, updated_by_id)
            VALUES
                ({campaignId}, {eventId}, 'registration_invite', 'es', {CampaignState.Sent},
                 now(), now(), {actor}, {actor})
            """);

        await ExecuteAsync(factory, $"""
            INSERT INTO convocatorias.campaign_recipient
                (id, campaign_id, contact_id, first_name, last_name, email, send_status,
                 registration_token_hash, created_at, updated_at)
            VALUES
                ({recipientId}, {campaignId}, {Guid.CreateVersion7()}, {firstName}, {lastName},
                 {email}, {RecipientSendStatus.HandedOff}, {token.Hash}, now(), now())
            """);

        return new SeededInvitation(token.Plaintext, recipientId, campaignId, email, firstName, lastName);
    }

    public static Task<HttpResponseMessage> ResolveAsync(HttpClient client, string? token) =>
        client.PostAsJsonAsync(ResolveRoute, new { token });

    /// <summary>
    /// Manda el formulario. <paramref name="token"/> nulo es el camino de quien llega desde la
    /// cartelera sin haber recibido ningún correo (spec <c>06</c> §1.1).
    /// </summary>
    public static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        Guid eventId,
        string? token = null,
        string email = "visitante@acme.com",
        string firstName = "Luis",
        string lastName = "Peña",
        string? phone = "+1 809 555 0100",
        string? company = "Acme",
        string? jobTitle = "Director",
        string? country = "República Dominicana") =>
        client.PostAsJsonAsync(RegisterRoute, new
        {
            eventId,
            token,
            firstName,
            lastName,
            email,
            phone,
            company,
            jobTitle,
            country,
        });

    /// <summary>Cuántas inscripciones tiene un evento.</summary>
    public static async Task<int> CountAsync(ConvocatoriasApiDbFactory factory, Guid eventId) =>
        (await QueryAsync<int>(factory, $"""
            SELECT COUNT(*)::int AS "Value"
            FROM convocatorias.registration
            WHERE event_id = {eventId}
            """)).Single();

    /// <summary>A qué destinatario quedó atada una inscripción, o nulo si fue una entrada libre.</summary>
    public static async Task<Guid?> ReferringRecipientAsync(
        ConvocatoriasApiDbFactory factory,
        Guid eventId,
        string emailNormalized) =>
        (await QueryAsync<Guid?>(factory, $"""
            SELECT campaign_recipient_id AS "Value"
            FROM convocatorias.registration
            WHERE event_id = {eventId} AND email_normalized = {emailNormalized}
            """)).Single();

    /// <summary>El nombre con el que quedó escrita, para comprobar que un reenvío no lo pisa.</summary>
    public static async Task<string> FirstNameAsync(
        ConvocatoriasApiDbFactory factory,
        Guid eventId,
        string emailNormalized) =>
        (await QueryAsync<string>(factory, $"""
            SELECT first_name AS "Value"
            FROM convocatorias.registration
            WHERE event_id = {eventId} AND email_normalized = {emailNormalized}
            """)).Single();

    /// <summary>
    /// Cómo quedó una inscripción respecto del CRM: el contacto, el sello del fallo y la marca de
    /// discrepancia (spec <c>06</c> §4.2 y §4.3). Se lee de las columnas y no de la respuesta
    /// porque nada de esto sale por el portal: al desconocido que se inscribió no le importa.
    /// </summary>
    public static async Task<RegistrationCrmSync> CrmSyncAsync(
        ConvocatoriasApiDbFactory factory,
        Guid eventId,
        string emailNormalized)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        return await context.Database
            .SqlQuery<RegistrationCrmSync>($"""
                SELECT crm_contact_id AS "ContactId",
                       crm_sync_failed_at AS "FailedAt",
                       contact_details_differ AS "DetailsDiffer"
                FROM convocatorias.registration
                WHERE event_id = {eventId} AND email_normalized = {emailNormalized}
                """)
            .SingleAsync();
    }

    /// <summary>
    /// Las inscripciones de un evento que están pendientes de sincronizar, <b>derivadas de las
    /// dos columnas anulables</b> y de nada más. Es la lectura que va a hacer <c>07</c>, escrita
    /// acá como SQL para que el test la cobre sin que exista todavía su endpoint.
    /// </summary>
    public static Task<IReadOnlyList<string>> PendingCrmSyncEmailsAsync(
        ConvocatoriasApiDbFactory factory,
        Guid eventId) =>
        QueryAsync<string>(factory, $"""
            SELECT email_normalized AS "Value"
            FROM convocatorias.registration
            WHERE event_id = {eventId}
              AND crm_contact_id IS NULL
              AND crm_sync_failed_at IS NOT NULL
            """);

    /// <summary>
    /// Todo el texto de la tabla, columna a columna. Es con lo que se comprueba que ningún token
    /// en claro quedó guardado en ninguna parte.
    /// </summary>
    public static Task<IReadOnlyList<string>> EveryTextValueAsync(ConvocatoriasApiDbFactory factory) =>
        QueryAsync<string>(factory, $"""
            SELECT unnest(ARRAY[
                first_name, last_name, email, email_normalized,
                COALESCE(phone, ''), COALESCE(company, ''), COALESCE(job_title, ''),
                COALESCE(country, ''), reference, qr_code
            ]) AS "Value"
            FROM convocatorias.registration
            """);

    private static async Task<IReadOnlyList<T>> QueryAsync<T>(
        ConvocatoriasApiDbFactory factory,
        FormattableString sql)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        return await context.Database.SqlQuery<T>(sql).ToListAsync();
    }

    private static async Task ExecuteAsync(ConvocatoriasApiDbFactory factory, FormattableString sql)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        await context.Database.ExecuteSqlAsync(sql);
    }
}

/// <summary>
/// Cómo quedó una inscripción respecto del CRM. <b>Los dos estados de §4.3 salen de la pareja</b>:
/// con contacto está sincronizada, y con sello y sin contacto está pendiente.
/// </summary>
/// <param name="ContactId">El contacto del CRM al que quedó atada, o nulo.</param>
/// <param name="FailedAt">Cuándo el CRM no contestó, o nulo.</param>
/// <param name="DetailsDiffer">Si lo que escribió no coincide con la ficha que ya estaba.</param>
internal sealed record RegistrationCrmSync(Guid? ContactId, DateTimeOffset? FailedAt, bool DetailsDiffer);

/// <summary>Una invitación ya mandada, con el texto plano que solo el test conoce.</summary>
/// <param name="Token">El token en claro, tal como iría en el enlace del correo.</param>
/// <param name="RecipientId">La fila de <c>campaign_recipient</c> que lo guarda hasheado.</param>
/// <param name="CampaignId">Su campaña.</param>
/// <param name="Email">La dirección a la que se mandó.</param>
/// <param name="FirstName">El nombre con el que está en la lista.</param>
/// <param name="LastName">El apellido.</param>
internal sealed record SeededInvitation(
    string Token,
    Guid RecipientId,
    Guid CampaignId,
    string Email,
    string FirstName,
    string LastName);
