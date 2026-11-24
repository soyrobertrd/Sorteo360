using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Registrations;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.PublicEndpoints;

/// <summary>
/// El correo de confirmación de una inscripción (spec <c>06</c> §4.4): se manda sincrónico dentro
/// del POST, en el idioma del evento, con la referencia y la URL del QR, y nada de la campaña. El
/// transporte por omisión de los tests es el stub, que lo guarda en <c>stub_notification</c>, así
/// que "aterriza en Mailpit" se comprueba leyendo esa tabla.
/// <para>
/// <b>Cada test sirve su propio evento</b>, con id propio: la base vive toda la clase y la
/// unicidad es de la base.
/// </para>
/// </summary>
public sealed class RegistrationConfirmationTests : IClassFixture<RegistrationPortalFactory>
{
    private readonly RegistrationPortalFactory _factory;

    public RegistrationConfirmationTests(RegistrationPortalFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_ReturnsAConfirmationInTheEventLanguage_WhenARegistrationSucceeds()
    {
        var eventId = Guid.NewGuid();
        _factory.Events.ServeEvents(PublicEvent(eventId));
        var client = _factory.CreateClient();
        var email = Unique();

        var response = await RegistrationTestClient.RegisterAsync(client, eventId, token: null, email: email);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var confirmation = Single(await ConfirmationsForAsync(email));
        confirmation.Type.ShouldBe(RegistrationConfirmationSender.NotificationType);
        confirmation.Language.ShouldBe("es");
        confirmation.Status.ShouldBe(CampaignNotification.StatusAccepted);
    }

    [Fact]
    public async Task Register_ReturnsTheReferenceAndQrInTheConfirmationBody_WhenARegistrationSucceeds()
    {
        var eventId = Guid.NewGuid();
        _factory.Events.ServeEvents(PublicEvent(eventId));
        var client = _factory.CreateClient();
        var email = Unique();

        var response = await RegistrationTestClient.RegisterAsync(client, eventId, token: null, email: email);
        var codes = await CodesAsync(response);

        var confirmation = Single(await ConfirmationsForAsync(email));

        // Sustituido de punta a punta: sin un solo marcador crudo.
        confirmation.BodyHtml.ShouldNotContain("{{");
        // La referencia como texto y la URL de la imagen del QR (spec 06 §4.4).
        confirmation.BodyHtml.ShouldContain(codes.Reference);
        confirmation.BodyHtml.ShouldContain($"/v1/public/registrations/{codes.QrCode}/qr");
        // Y el enlace a la página de la entrada, para quien prefiere un enlace a una imagen que su
        // cliente de correo puede bloquear (spec 06 §4.4).
        confirmation.BodyHtml.ShouldContain($"/publico/entrada/{codes.QrCode}");
        // Nada de la campaña: una confirmación no ofrece darse de baja (spec 06 §4.4).
        confirmation.BodyHtml.ShouldNotContain("enlace_baja");
        confirmation.BodyHtml.ShouldNotContain("/publico/baja");
    }

    [Fact]
    public async Task Register_ReturnsASecondConfirmationAndNoNewRow_WhenTheSamePersonRegistersTwice()
    {
        var eventId = Guid.NewGuid();
        _factory.Events.ServeEvents(PublicEvent(eventId));
        var client = _factory.CreateClient();
        var email = Unique();

        (await RegistrationTestClient.RegisterAsync(client, eventId, token: null, email: email))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        // "Perdí el correo": el reenvío es 200 y vuelve a mandar la confirmación (spec 06 §4.1).
        (await RegistrationTestClient.RegisterAsync(client, eventId, token: null, email: email))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await RegistrationTestClient.CountAsync(_factory, eventId)).ShouldBe(1);

        var confirmations = await ConfirmationsForAsync(email);
        confirmations.Count.ShouldBe(2);
        // Reenviada sin cambios: mismo cuerpo las dos veces.
        confirmations[0].BodyHtml.ShouldBe(confirmations[1].BodyHtml);
    }

    [Fact]
    public async Task Register_ReturnsTheConfirmation_WhenTheRegistrantIsSuppressed()
    {
        var eventId = Guid.NewGuid();
        _factory.Events.ServeEvents(PublicEvent(eventId));
        _factory.Crm.ServeContacts(new StubContact(
            CrmStub.SuppressedContactId, "Luis", "Gómez", CrmStub.SuppressedContactEmail,
            CanBeEmailed: false, SuppressionReason: CrmStub.OptOutSuppressionReason));
        var client = _factory.CreateClient();

        // La supresión gobierna lo que mandamos sin que nos lo pidan, no la respuesta a un acto de
        // la propia persona (spec 06 §6). Quien se dio de baja del marketing igual tiene que
        // recibir su confirmación, o se queda sin QR.
        var response = await RegistrationTestClient.RegisterAsync(
            client, eventId, token: null, email: CrmStub.SuppressedContactEmail);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        Single(await ConfirmationsForAsync(CrmStub.SuppressedContactEmail))
            .Type.ShouldBe(RegistrationConfirmationSender.NotificationType);
    }

    private static StubEvent PublicEvent(Guid id) => new(
        id, "Roadshow Madrid 2026", EventsStub.PublicVisibility, "es", StubEventStatus.InProgress);

    private static string Unique() => EmailAddressShape.Normalize($"p{Guid.NewGuid():N}@acme.com");

    private static ConfirmationRow Single(IReadOnlyList<ConfirmationRow> rows) => rows.ShouldHaveSingleItem();

    private static async Task<ResponseCodes> CodesAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return new ResponseCodes(
            body.GetProperty("reference").GetString()!,
            body.GetProperty("qrCode").GetString()!);
    }

    /// <summary>
    /// Las filas de <c>stub_notification</c> de un destinatario cuyo tipo es la confirmación, en
    /// orden de creación. Es donde el transporte stub deja lo que habría salido (spec <c>04</c> §4.1).
    /// </summary>
    private async Task<IReadOnlyList<ConfirmationRow>> ConfirmationsForAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        return await context.Database
            .SqlQuery<ConfirmationRow>($"""
                SELECT type AS "Type",
                       language AS "Language",
                       subject AS "Subject",
                       body_html AS "BodyHtml",
                       status AS "Status"
                FROM convocatorias.stub_notification
                WHERE recipient_email = {email}
                  AND type = {RegistrationConfirmationSender.NotificationType}
                ORDER BY created_at
                """)
            .ToListAsync();
    }

    private sealed record ConfirmationRow(string Type, string Language, string Subject, string BodyHtml, string Status);

    private sealed record ResponseCodes(string Reference, string QrCode);
}
