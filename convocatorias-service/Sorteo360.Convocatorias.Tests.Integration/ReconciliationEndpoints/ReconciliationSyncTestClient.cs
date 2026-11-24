using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;

namespace Sorteo360.Convocatorias.Tests.Integration.ReconciliationEndpoints;

/// <summary>
/// Lo que repiten los tests de los reintentos: las dos rutas y una inscripción varada sembrada por
/// SQL, con el sello del fallo puesto y sin contacto, que es lo que la ruta pública deja cuando el
/// CRM no contesta (spec <c>06</c> §4.3).
/// </summary>
internal static class ReconciliationSyncTestClient
{
    public static Task<HttpResponseMessage> CrmSyncAsync(HttpClient client, Guid registrationId) =>
        client.PostAsync($"/v1/convocatorias/registrations/{registrationId}/crm-sync", content: null);

    public static Task<HttpResponseMessage> UnsubscribeSyncAsync(HttpClient client, Guid recipientId) =>
        client.PostAsync($"/v1/convocatorias/recipients/{recipientId}/unsubscribe-sync", content: null);

    /// <summary>Una inscripción pendiente de sincronizar, con una dirección que no usó nadie más.</summary>
    public static async Task<PendingRegistration> SeedPendingRegistrationAsync(
        ConvocatoriasApiDbFactory factory,
        string firstName = "Luis",
        string lastName = "Peña",
        string? email = null)
    {
        var id = Guid.CreateVersion7();
        var address = email ?? $"p{Guid.NewGuid():N}@acme.com";

        await ExecuteAsync(factory, $"""
            INSERT INTO convocatorias.registration
                (id, event_id, first_name, last_name, email, email_normalized, reference, qr_code,
                 contact_details_differ, crm_sync_failed_at, created_at, updated_at)
            VALUES
                ({id}, {Guid.NewGuid()}, {firstName}, {lastName}, {address},
                 {EmailAddressShape.Normalize(address)}, {$"MITUR-2026-{Guid.NewGuid():N}"},
                 {Guid.NewGuid().ToString("N")}, false, now(), now(), now())
            """);

        return new PendingRegistration(id, address);
    }

    /// <summary>Marca una fila de destinatario como dada de baja y sin sincronizar.</summary>
    public static Task MarkUnsubscribePendingAsync(ConvocatoriasApiDbFactory factory, Guid recipientId) =>
        ExecuteAsync(factory, $"""
            UPDATE convocatorias.campaign_recipient
            SET unsubscribed_at = now() - interval '1 day', unsubscribe_synced_at = NULL
            WHERE id = {recipientId}
            """);

    /// <summary>Cómo quedó una inscripción respecto del CRM, leída de sus columnas.</summary>
    public static async Task<RegistrationCrmState> CrmStateAsync(
        ConvocatoriasApiDbFactory factory,
        Guid registrationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        return await context.Database
            .SqlQuery<RegistrationCrmState>($"""
                SELECT crm_contact_id AS "ContactId", crm_sync_failed_at AS "FailedAt"
                FROM convocatorias.registration
                WHERE id = {registrationId}
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

/// <summary>Una inscripción varada sembrada para el test.</summary>
/// <param name="Id">Su id, que es lo que lleva la ruta.</param>
/// <param name="Email">La dirección con la que se inscribió.</param>
internal sealed record PendingRegistration(Guid Id, string Email);

/// <summary>Las dos columnas de §4.3: con contacto está sincronizada, con sello y sin contacto, pendiente.</summary>
/// <param name="ContactId">El contacto del CRM, o nulo.</param>
/// <param name="FailedAt">Cuándo el CRM no contestó, o nulo.</param>
internal sealed record RegistrationCrmState(Guid? ContactId, DateTimeOffset? FailedAt);
