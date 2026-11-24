using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Portal;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;

namespace Sorteo360.Convocatorias.Tests.Integration.PublicEndpoints;

/// <summary>
/// Lo que repiten los tests de la baja: un destinatario con su token de baja ya sembrado, la
/// llamada del enlace, y la lectura de las dos columnas anulables.
/// <para>
/// <b>Se siembra por SQL y no lanzando una campaña.</b> Lo que hace falta es un
/// <c>campaign_recipient</c> con un hash de token de baja y su <c>contact_id</c>; llegar a eso
/// por la API pediría crear, seleccionar, lanzar y esperar al worker, cuyos fallos aparecerían
/// como fallos de esta ruta. Que la entrega escriba ese hash ya lo cobra <c>CampaignHandoffTests</c>.
/// </para>
/// </summary>
internal static class UnsubscribeTestClient
{
    public const string UnsubscribeRoute = "/v1/public/unsubscribe";

    public static Task<HttpResponseMessage> UnsubscribeAsync(HttpClient client, string? token) =>
        client.PostAsJsonAsync(UnsubscribeRoute, new { token });

    /// <summary>
    /// Deja sembrado un destinatario que puede darse de baja y devuelve el token en claro, que es
    /// lo que llevaría el fragmento <c>#u=</c> del correo. El texto plano lo acuña el test: la base
    /// guarda solo el hash (spec <c>06</c> §2.1).
    /// </summary>
    public static async Task<SeededUnsubscribe> InviteAsync(
        ConvocatoriasApiDbFactory factory,
        Guid? contactId = null,
        string email = "invitada@acme.com")
    {
        var token = PortalToken.Mint();
        var campaignId = Guid.CreateVersion7();
        var recipientId = Guid.CreateVersion7();
        var actor = Guid.CreateVersion7();
        var contact = contactId ?? Guid.CreateVersion7();

        await ExecuteAsync(factory, $"""
            INSERT INTO convocatorias.campaign
                (id, event_id, template_key, language, state,
                 created_at, updated_at, created_by_id, updated_by_id)
            VALUES
                ({campaignId}, {Guid.CreateVersion7()}, 'registration_invite', 'es', {CampaignState.Sent},
                 now(), now(), {actor}, {actor})
            """);

        await ExecuteAsync(factory, $"""
            INSERT INTO convocatorias.campaign_recipient
                (id, campaign_id, contact_id, first_name, last_name, email, send_status,
                 unsubscribe_token_hash, created_at, updated_at)
            VALUES
                ({recipientId}, {campaignId}, {contact}, 'Ana', 'Reyes',
                 {email}, {RecipientSendStatus.HandedOff}, {token.Hash}, now(), now())
            """);

        return new SeededUnsubscribe(token.Plaintext, recipientId, contact, email);
    }

    /// <summary>
    /// Cómo quedó la baja de una fila: cuándo se hizo clic y cuándo llegó al CRM. Los dos estados
    /// —hecha y pendiente— salen de la pareja, sin ninguna columna de estado (spec <c>06</c> §6).
    /// </summary>
    public static async Task<UnsubscribeState> StateAsync(ConvocatoriasApiDbFactory factory, Guid recipientId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        return await context.Database
            .SqlQuery<UnsubscribeState>($"""
                SELECT unsubscribed_at AS "UnsubscribedAt",
                       unsubscribe_synced_at AS "SyncedAt"
                FROM convocatorias.campaign_recipient
                WHERE id = {recipientId}
                """)
            .SingleAsync();
    }

    private static async Task ExecuteAsync(ConvocatoriasApiDbFactory factory, FormattableString sql)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        await context.Database.ExecuteSqlAsync(sql);
    }
}

/// <summary>Un destinatario sembrado que puede darse de baja, con el texto plano que solo el test conoce.</summary>
/// <param name="Token">El token de baja en claro, tal como iría en el fragmento <c>#u=</c>.</param>
/// <param name="RecipientId">La fila de <c>campaign_recipient</c> que lo guarda hasheado.</param>
/// <param name="ContactId">El contacto del CRM al que se le empuja la baja.</param>
/// <param name="Email">La dirección a la que se escribió.</param>
internal sealed record SeededUnsubscribe(string Token, Guid RecipientId, Guid ContactId, string Email);

/// <summary>
/// Cómo quedó una baja. <b>Los dos estados de §6 salen de la pareja</b>: con sello está
/// sincronizada, y con clic y sin sello está pendiente.
/// </summary>
/// <param name="UnsubscribedAt">Cuándo se hizo clic, o nulo si nadie se dio de baja.</param>
/// <param name="SyncedAt">Cuándo llegó la baja al CRM, o nulo si sigue pendiente.</param>
internal sealed record UnsubscribeState(DateTimeOffset? UnsubscribedAt, DateTimeOffset? SyncedAt);
