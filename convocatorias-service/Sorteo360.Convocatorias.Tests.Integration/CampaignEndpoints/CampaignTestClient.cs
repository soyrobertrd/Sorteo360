using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.CampaignEndpoints;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.CampaignEndpoints;

/// <summary>
/// Lo que las tres suites de campañas repiten: armar un borrador, leerlo, y empujar la base al
/// estado que hace falta para probar un rechazo.
/// <para>
/// Las escrituras directas van por SQL y no por el <c>DbContext</c> porque sus <c>DbSet</c> son
/// internos a Infrastructure a propósito: la API es la puerta, y un test que la rodeara con el
/// modelo tipado sería el primer sitio donde esa puerta deja de ser la única.
/// </para>
/// </summary>
internal static class CampaignTestClient
{
    public const string Route = "/v1/convocatorias/campaigns";

    /// <summary>Una plantilla de campaña de las que siembra la migración.</summary>
    public const string TemplateKey = "registration_invite";

    public static async Task<CampaignResponse> CreateAsync(
        HttpClient client,
        Guid eventId,
        string templateKey = TemplateKey,
        string language = "es")
    {
        var response = await client.PostAsJsonAsync(Route, new
        {
            eventId,
            templateKey,
            language,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await response.Content.ReadFromJsonAsync<CampaignResponse>()
            ?? throw new InvalidOperationException("La creación no devolvió campaña.");
    }

    /// <summary>Un mensaje directo: sin evento, con su asunto y su texto (PGTI-174).</summary>
    public static async Task<CampaignResponse> CreateDirectMessageAsync(
        HttpClient client,
        string subject = "Novedades del roadshow",
        string message = "Gracias por su interés.")
    {
        var response = await client.PostAsJsonAsync(Route, new
        {
            templateKey = Core.Templates.TemplateKey.DirectMessage,
            language = "es",
            subject,
            message,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await response.Content.ReadFromJsonAsync<CampaignResponse>()
            ?? throw new InvalidOperationException("La creación no devolvió campaña.");
    }

    /// <summary>
    /// Los dos hashes de cada destinatario de una campaña, sin ordenar, como un par de "tiene o no
    /// tiene": el valor no le sirve a ningún test y su ausencia es lo que se mira.
    /// </summary>
    public static async Task<IReadOnlyList<RecipientTokens>> RecipientTokensAsync(
        ConvocatoriasApiDbFactory factory,
        Guid campaignId) =>
        await QueryAsync<RecipientTokens>(factory, $"""
            SELECT registration_token_hash IS NOT NULL AS "HasRegistrationToken",
                   unsubscribe_token_hash IS NOT NULL AS "HasUnsubscribeToken"
            FROM convocatorias.campaign_recipient
            WHERE campaign_id = {campaignId}
            """);

    public static async Task<CampaignResponse> GetAsync(HttpClient client, Guid id)
    {
        var response = await client.GetAsync($"{Route}/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<CampaignResponse>()
            ?? throw new InvalidOperationException("La lectura no devolvió campaña.");
    }

    /// <summary>
    /// Selecciona con el filtro vacío, que es el que trae todo lo que sirva el CRM simulado.
    /// </summary>
    public static Task<HttpResponseMessage> SelectAsync(HttpClient client, Guid id) =>
        client.PostAsJsonAsync($"{Route}/{id}/recipients/select", new { });

    public static Task<HttpResponseMessage> LaunchAsync(HttpClient client, Guid id, object body) =>
        client.PostAsJsonAsync($"{Route}/{id}/launch", body);

    public static Task<HttpResponseMessage> CancelAsync(HttpClient client, Guid id) =>
        client.PostAsync($"{Route}/{id}/cancel", content: null);

    public static Task<HttpResponseMessage> ResumeHandoffAsync(HttpClient client, Guid id) =>
        client.PostAsync($"{Route}/{id}/resume-handoff", content: null);

    public static Task<HttpResponseMessage> RetryFailedAsync(HttpClient client, Guid id) =>
        client.PostAsync($"{Route}/{id}/retry-failed", content: null);

    /// <summary>
    /// El camino entero hasta el <c>202</c>: un borrador, su lista sacada del CRM simulado, y el
    /// lanzamiento con la cuenta que la propia campaña dice tener. Devuelve la campaña tal como
    /// estaba justo antes de lanzar.
    /// </summary>
    public static async Task<CampaignResponse> CreateAndLaunchAsync(HttpClient client, Guid eventId)
    {
        var campaign = await CreateAsync(client, eventId);

        (await SelectAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        return await LaunchReadyAsync(client, campaign.Id);
    }

    /// <summary>El lanzamiento de una campaña a la que ya se le armó la lista.</summary>
    public static async Task<CampaignResponse> LaunchReadyAsync(HttpClient client, Guid campaignId)
    {
        var withRecipients = await GetAsync(client, campaignId);
        withRecipients.RecipientCount.ShouldBeGreaterThan(0);

        (await LaunchAsync(
            client,
            campaignId,
            new { expectedRecipientCount = withRecipients.RecipientCount })).StatusCode
            .ShouldBe(HttpStatusCode.Accepted);

        return withRecipients;
    }

    /// <summary>
    /// Sondea hasta <c>sent</c>, que es lo que hace la pantalla (spec <c>04</c> §1.2). La entrega
    /// corre en un worker, así que afirmar enseguida sería una carrera contra él.
    /// </summary>
    public static async Task WaitUntilSentAsync(HttpClient client, Guid campaignId, int seconds = 30)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(seconds);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if ((await GetAsync(client, campaignId)).State == CampaignState.Sent)
                return;

            await Task.Delay(100);
        }

        throw new TimeoutException($"La campaña {campaignId} no llegó a '{CampaignState.Sent}'.");
    }

    /// <summary>
    /// Marca a todos los destinatarios de una campaña como dados de baja. Es la forma de llegar
    /// a "hay lista y no se puede contactar a nadie" antes de que exista el portal que pone esa
    /// columna.
    /// </summary>
    public static Task UnsubscribeEveryRecipientAsync(ConvocatoriasApiDbFactory factory, Guid campaignId) =>
        ExecuteAsync(
            factory,
            $"UPDATE convocatorias.campaign_recipient SET unsubscribed_at = now() WHERE campaign_id = {campaignId}");

    /// <summary>
    /// Marca a un destinatario concreto como dado de baja, sin sincronizar. Es el estado que el
    /// portal de la spec <c>06</c> dejará cuando alguien haga clic en su enlace, y el que el
    /// vaciado del lanzamiento tiene que empujarle al CRM (spec <c>04</c> §3.1).
    /// </summary>
    public static Task UnsubscribeRecipientAsync(
        ConvocatoriasApiDbFactory factory,
        Guid campaignId,
        Guid contactId) =>
        ExecuteAsync(
            factory,
            $"""
            UPDATE convocatorias.campaign_recipient
            SET unsubscribed_at = now(), unsubscribe_synced_at = NULL
            WHERE campaign_id = {campaignId} AND contact_id = {contactId}
            """);

    /// <summary>
    /// Siembra la variante de una plantilla de campaña en otro idioma. La migración solo siembra
    /// <c>es</c>, y crear o editar una campaña en otro idioma exige que su plantilla exista (spec
    /// <c>03</c> §4.1). Idempotente: sembrar dos veces la misma pareja no falla.
    /// </summary>
    public static Task SeedCampaignTemplateAsync(
        ConvocatoriasApiDbFactory factory,
        string templateKey,
        string language) =>
        ExecuteAsync(
            factory,
            $"""
            INSERT INTO convocatorias.template (template_key, language, subject, body_html, purpose, is_campaign_selectable)
            VALUES ({templateKey}, {language}, 'Asunto', '<p>Hola</p>', 'campaign', true)
            ON CONFLICT (template_key, language) DO NOTHING
            """);

    /// <summary>
    /// Cambia el idioma de una campaña por debajo de la API. Es la única forma de llegar a un
    /// borrador cuya pareja no tiene plantilla —el que se guardó antes de que crear y editar lo
    /// rechazaran— y así probar que el lanzamiento lo rechaza igual.
    /// </summary>
    public static Task SetCampaignLanguageAsync(ConvocatoriasApiDbFactory factory, Guid campaignId, string language) =>
        ExecuteAsync(
            factory,
            $"UPDATE convocatorias.campaign SET language = {language} WHERE id = {campaignId}");

    /// <summary>
    /// Sella todas las bajas pendientes del servicio, sin pasar por el CRM. Es cómo un test
    /// declara su premisa —"acá no hay nada que vaciar"— en vez de heredarla del test anterior:
    /// el vaciado del lanzamiento mira <b>todo</b> el servicio, no solo la campaña que se lanza.
    /// </summary>
    public static Task MarkEveryUnsubscribeSyncedAsync(ConvocatoriasApiDbFactory factory) =>
        ExecuteAsync(
            factory,
            $"""
            UPDATE convocatorias.campaign_recipient
            SET unsubscribe_synced_at = now()
            WHERE unsubscribed_at IS NOT NULL AND unsubscribe_synced_at IS NULL
            """);

    /// <summary>
    /// Cuántas bajas de esta campaña siguen sin sellar. Cero después de un lanzamiento es lo que
    /// dice que el CRM ya se enteró.
    /// </summary>
    public static async Task<int> CountUnsyncedUnsubscribesAsync(
        ConvocatoriasApiDbFactory factory,
        Guid campaignId) =>
        (await QueryAsync<int>(factory, $"""
            SELECT COUNT(*)::int AS "Value"
            FROM convocatorias.campaign_recipient
            WHERE campaign_id = {campaignId}
              AND unsubscribed_at IS NOT NULL
              AND unsubscribe_synced_at IS NULL
            """)).Single();

    /// <summary>
    /// El estado de envío de un destinatario concreto, y el motivo con el que se anotó. Van por
    /// separado y no en una fila porque <c>SqlQuery</c> solo materializa escalares.
    /// </summary>
    public static async Task<string> RecipientSendStatusAsync(
        ConvocatoriasApiDbFactory factory,
        Guid campaignId,
        Guid contactId) =>
        (await QueryAsync<string>(factory, $"""
            SELECT send_status AS "Value"
            FROM convocatorias.campaign_recipient
            WHERE campaign_id = {campaignId} AND contact_id = {contactId}
            """)).Single();

    /// <inheritdoc cref="RecipientSendStatusAsync" />
    public static async Task<string?> RecipientFailureReasonAsync(
        ConvocatoriasApiDbFactory factory,
        Guid campaignId,
        Guid contactId) =>
        (await QueryAsync<string?>(factory, $"""
            SELECT failure_reason AS "Value"
            FROM convocatorias.campaign_recipient
            WHERE campaign_id = {campaignId} AND contact_id = {contactId}
            """)).Single();

    /// <summary>Cuántas exclusiones tiene una campaña por un motivo concreto.</summary>
    public static async Task<int> CountExclusionsAsync(
        ConvocatoriasApiDbFactory factory,
        Guid campaignId,
        string reason)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        var counts = await context.Database
            .SqlQuery<int>($"""
                SELECT COUNT(*)::int AS "Value"
                FROM convocatorias.campaign_exclusion
                WHERE campaign_id = {campaignId} AND reason = {reason}
                """)
            .ToListAsync();

        return counts.Single();
    }

    /// <summary>Los estados de envío de los destinatarios de una campaña, sin ordenar.</summary>
    public static async Task<IReadOnlyList<string>> RecipientStatusesAsync(
        ConvocatoriasApiDbFactory factory,
        Guid campaignId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        return await context.Database
            .SqlQuery<string>($"""
                SELECT send_status AS "Value"
                FROM convocatorias.campaign_recipient
                WHERE campaign_id = {campaignId}
                """)
            .ToListAsync();
    }

    /// <summary>Los ids de los destinatarios de una campaña.</summary>
    public static async Task<IReadOnlyList<Guid>> RecipientIdsAsync(
        ConvocatoriasApiDbFactory factory,
        Guid campaignId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        return await context.Database
            .SqlQuery<Guid>($"""
                SELECT id AS "Value"
                FROM convocatorias.campaign_recipient
                WHERE campaign_id = {campaignId}
                """)
            .ToListAsync();
    }

    /// <summary>Las direcciones de los destinatarios de una campaña, sin ordenar.</summary>
    public static Task<IReadOnlyList<string>> RecipientEmailsAsync(
        ConvocatoriasApiDbFactory factory,
        Guid campaignId) =>
        QueryAsync<string>(factory, $"""
            SELECT email AS "Value"
            FROM convocatorias.campaign_recipient
            WHERE campaign_id = {campaignId}
            """);

    /// <summary>
    /// Los tres valores del evento copiados en la fila de la campaña. Nulos mientras sea un
    /// borrador: los estampa el lanzamiento (spec <c>04</c> §1 paso 4).
    /// </summary>
    public static async Task<(string? Name, string? Location, string? Dates)> EventValuesAsync(
        ConvocatoriasApiDbFactory factory,
        Guid campaignId)
    {
        var name = await ScalarAsync<string?>(factory, $"""
            SELECT event_name AS "Value" FROM convocatorias.campaign WHERE id = {campaignId}
            """);
        var location = await ScalarAsync<string?>(factory, $"""
            SELECT event_location AS "Value" FROM convocatorias.campaign WHERE id = {campaignId}
            """);
        var dates = await ScalarAsync<string?>(factory, $"""
            SELECT event_dates AS "Value" FROM convocatorias.campaign WHERE id = {campaignId}
            """);

        return (name, location, dates);
    }

    /// <summary>
    /// Los dos hashes de un destinatario. Es lo único que la base guarda de sus tokens: el texto
    /// plano se fue dentro del enlace y no se vuelve a necesitar (spec <c>06</c> §2.1).
    /// </summary>
    public static async Task<(byte[]? Registration, byte[]? Unsubscribe)> RecipientTokenHashesAsync(
        ConvocatoriasApiDbFactory factory,
        Guid campaignId,
        Guid recipientId)
    {
        var registration = await ScalarAsync<byte[]?>(factory, $"""
            SELECT registration_token_hash AS "Value"
            FROM convocatorias.campaign_recipient
            WHERE campaign_id = {campaignId} AND id = {recipientId}
            """);
        var unsubscribe = await ScalarAsync<byte[]?>(factory, $"""
            SELECT unsubscribe_token_hash AS "Value"
            FROM convocatorias.campaign_recipient
            WHERE campaign_id = {campaignId} AND id = {recipientId}
            """);

        return (registration, unsubscribe);
    }

    private static async Task<T> ScalarAsync<T>(ConvocatoriasApiDbFactory factory, FormattableString sql) =>
        (await QueryAsync<T>(factory, sql)).Single();

    /// <summary>Cuántas cargas sin confirmar tiene una campaña. El índice parcial dice que una.</summary>
    public static async Task<int> CountPendingUploadsAsync(ConvocatoriasApiDbFactory factory, Guid campaignId) =>
        (await QueryAsync<int>(factory, $"""
            SELECT COUNT(*)::int AS "Value"
            FROM convocatorias.campaign_upload_batch
            WHERE campaign_id = {campaignId} AND confirmed_at IS NULL
            """)).Single();

    /// <summary>Cuántas cargas tiene en total, confirmadas incluidas.</summary>
    public static async Task<int> CountUploadsAsync(ConvocatoriasApiDbFactory factory, Guid campaignId) =>
        (await QueryAsync<int>(factory, $"""
            SELECT COUNT(*)::int AS "Value"
            FROM convocatorias.campaign_upload_batch
            WHERE campaign_id = {campaignId}
            """)).Single();

    /// <summary>Cuántas filas revisadas quedan bajo las cargas de una campaña.</summary>
    public static async Task<int> CountUploadRowsAsync(ConvocatoriasApiDbFactory factory, Guid campaignId) =>
        (await QueryAsync<int>(factory, $"""
            SELECT COUNT(*)::int AS "Value"
            FROM convocatorias.campaign_upload_row r
            JOIN convocatorias.campaign_upload_batch b ON b.id = r.batch_id
            WHERE b.campaign_id = {campaignId}
            """)).Single();

    private static async Task<IReadOnlyList<T>> QueryAsync<T>(ConvocatoriasApiDbFactory factory, FormattableString sql)
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
