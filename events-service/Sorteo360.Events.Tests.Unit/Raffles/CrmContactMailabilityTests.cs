using Microsoft.Extensions.Logging.Abstractions;
using Sorteo360.Events.Infrastructure.Crm;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Polly.CircuitBreaker;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

/// <summary>
/// La lista negra del sorteo vista desde el CRM: tandas de a mil, en serie, y todo o nada. Una
/// respuesta parcial no existe, porque quien quedara sin contestar se leería como borrado.
/// </summary>
public sealed class CrmContactMailabilityTests
{
    private readonly ICrmContactClient _client = Substitute.For<ICrmContactClient>();
    private readonly List<IReadOnlyList<Guid>> _batches = [];

    private CrmContactMailability CreateSut() => new(_client, NullLogger<CrmContactMailability>.Instance);

    public CrmContactMailabilityTests()
    {
        // Por omisión el CRM contesta por todos y no veta a nadie, y se anota cada tanda en orden.
        _client.CheckMailabilityAsync(Arg.Any<CrmMailabilityRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var ids = callInfo.Arg<CrmMailabilityRequest>().Ids;
                _batches.Add(ids);
                return Task.FromResult<IReadOnlyList<CrmMailabilityResponse>?>(
                    ids.Select(id => Invitable(id)).ToList());
            });
    }

    [Fact]
    public async Task CheckAsync_SendsBatchesOfAThousandInOrder_WhenThereAre2500Ids()
    {
        var ids = Ids(2500);

        var result = await CreateSut().CheckAsync(ids, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2500);
        _batches.Select(b => b.Count).ShouldBe([1000, 1000, 500]);
        _batches.SelectMany(b => b).ShouldBe(ids);
    }

    [Fact]
    public async Task CheckAsync_ReturnsEachInviteSuppressionReason_WhenTheCrmAnswers()
    {
        // El correo no entra: la vetada que además se dio de baja llega como opted_out en
        // suppressionReason, y lo que cuenta es la invitación (spec 01 §3).
        var (vetada, suspendida, desuscrita) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Answer(
            new CrmMailabilityResponse(vetada, false, "opted_out", false, "do_not_invite"),
            new CrmMailabilityResponse(suspendida, false, "no_email", false, "suspended"),
            new CrmMailabilityResponse(desuscrita, false, "opted_out", true, null));

        var result = await CreateSut().CheckAsync([vetada, suspendida, desuscrita, Guid.NewGuid()], CancellationToken.None);

        result.Value.ShouldBe(new Dictionary<Guid, string?>
        {
            [vetada] = "do_not_invite",
            [suspendida] = "suspended",
            [desuscrita] = null,
        });
    }

    [Fact]
    public async Task CheckAsync_ReturnsDoNotInvite_WhenTheCrmRefusesTheInvitationWithoutAReason()
    {
        // Quien decide es canBeInvited; sin motivo se nombra como la marca de no invitar.
        var contactId = Guid.NewGuid();
        Answer(new CrmMailabilityResponse(contactId, true, null, false, null));

        var result = await CreateSut().CheckAsync([contactId], CancellationToken.None);

        result.Value[contactId].ShouldBe("do_not_invite");
    }

    [Fact]
    public async Task CheckAsync_ReturnsInvitable_WhenTheCrmAllowsTheInvitationButSendsAReason()
    {
        var contactId = Guid.NewGuid();
        Answer(new CrmMailabilityResponse(contactId, true, null, true, "do_not_invite"));

        var result = await CreateSut().CheckAsync([contactId], CancellationToken.None);

        result.Value[contactId].ShouldBeNull();
    }

    [Fact]
    public async Task CheckAsync_ReturnsCrmUnavailable_WhenARowLacksCanBeInvited()
    {
        // Un CRM anterior a PGTI-190 no lo manda. Leído como false vaciaría el bombo en silencio.
        var (completa, incompleta) = (Guid.NewGuid(), Guid.NewGuid());
        Answer(
            Invitable(completa),
            new CrmMailabilityResponse(incompleta, true, null, CanBeInvited: null, InviteSuppressionReason: null));

        var result = await CreateSut().CheckAsync([completa, incompleta], CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CRM_UNAVAILABLE");
    }

    [Fact]
    public async Task CheckAsync_MakesNoCall_WhenThereAreNoIds()
    {
        var result = await CreateSut().CheckAsync([], CancellationToken.None);

        result.Value.ShouldBeEmpty();
        await _client.DidNotReceive().CheckMailabilityAsync(Arg.Any<CrmMailabilityRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAsync_ReturnsCrmUnavailableWithNothingPartial_WhenTheSecondBatchFails()
    {
        var calls = 0;
        _client.CheckMailabilityAsync(Arg.Any<CrmMailabilityRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => ++calls == 2
                ? throw new HttpRequestException("caído")
                : Task.FromResult<IReadOnlyList<CrmMailabilityResponse>?>(callInfo.Arg<CrmMailabilityRequest>().Ids
                    .Select(id => Invitable(id)).ToList()));

        var result = await CreateSut().CheckAsync(Ids(2500), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CRM_UNAVAILABLE");
        calls.ShouldBe(2);
    }

    [Fact]
    public async Task CheckAsync_ReturnsCrmUnavailable_WhenTheCrmAnswersWithAnEmptyBody()
    {
        _client.CheckMailabilityAsync(Arg.Any<CrmMailabilityRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CrmMailabilityResponse>?>(null));

        var result = await CreateSut().CheckAsync(Ids(3), CancellationToken.None);

        result.Error.Code.ShouldBe("CRM_UNAVAILABLE");
    }

    [Fact]
    public async Task CheckAsync_ReturnsCrmUnavailable_WhenTheBodyIsUnreadable()
    {
        // Así llega un 200 de cuerpo vacío o roto: ReadFromJsonAsync no devuelve null, lanza.
        _client.CheckMailabilityAsync(Arg.Any<CrmMailabilityRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new System.Text.Json.JsonException("cuerpo vacío"));

        var result = await CreateSut().CheckAsync(Ids(3), CancellationToken.None);

        result.Error.Code.ShouldBe("CRM_UNAVAILABLE");
    }

    [Fact]
    public async Task CheckAsync_ReturnsCrmUnavailable_WhenTheCircuitIsOpen()
    {
        // Así llega el circuito abierto desde el chasis: envuelto en la excepción de transporte.
        _client.CheckMailabilityAsync(Arg.Any<CrmMailabilityRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("circuito abierto", new BrokenCircuitException()));

        var result = await CreateSut().CheckAsync(Ids(3), CancellationToken.None);

        result.Error.Code.ShouldBe("CRM_UNAVAILABLE");
    }

    private void Answer(params CrmMailabilityResponse[] verdicts) =>
        _client.CheckMailabilityAsync(Arg.Any<CrmMailabilityRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CrmMailabilityResponse>?>(verdicts));

    private static CrmMailabilityResponse Invitable(Guid id) => new(id, true, null, true, null);

    private static Guid[] Ids(int count) => Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();
}
