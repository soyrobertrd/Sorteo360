using Microsoft.Extensions.Logging.Abstractions;
using Sorteo360.Events.Infrastructure.Keycloak;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Keycloak;

/// <summary>
/// Los nombres que el acta escribe para cada operador. La promesa es la del paso 4 de spec 03 §4.4:
/// un id que el realm no resuelve sale nulo, y completar no se cae nunca por eso.
/// </summary>
public class KeycloakRaffleOperatorDirectoryTests
{
    private static readonly Guid Ana = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Luis = new("22222222-2222-2222-2222-222222222222");

    private readonly IKeycloakAdminClient _client = Substitute.For<IKeycloakAdminClient>();

    private KeycloakRaffleOperatorDirectory CreateSut() =>
        new(_client, NullLogger<KeycloakRaffleOperatorDirectory>.Instance);

    [Fact]
    public async Task ResolveNamesAsync_ReturnsEachDisplayName_WhenTheRealmKnowsEveryone()
    {
        Serve(Ana, "Ana", "Pérez");
        Serve(Luis, "Luis", "Gómez");

        var names = await CreateSut().ResolveNamesAsync([Ana, Luis], CancellationToken.None);

        names[Ana].ShouldBe("Ana Pérez");
        names[Luis].ShouldBe("Luis Gómez");
    }

    [Fact]
    public async Task ResolveNamesAsync_ReturnsTheNameOfADisabledAccount_WhenItStillExists()
    {
        // Quien operó el sorteo lo operó: que hoy no pueda entrar no le borra el nombre del acta.
        _client.GetUserAsync(Ana.ToString(), Arg.Any<CancellationToken>())
            .Returns(new KeycloakUserResponse(Ana.ToString(), "ana", null, "Ana", "Pérez", Enabled: false));

        var names = await CreateSut().ResolveNamesAsync([Ana], CancellationToken.None);

        names[Ana].ShouldBe("Ana Pérez");
    }

    [Fact]
    public async Task ResolveNamesAsync_AsksOncePerPerson_WhenAnIdRepeats()
    {
        Serve(Ana, "Ana", "Pérez");

        var names = await CreateSut().ResolveNamesAsync([Ana, Ana, Ana], CancellationToken.None);

        names.Count.ShouldBe(1);
        await _client.Received(1).GetUserAsync(Ana.ToString(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveNamesAsync_ReturnsNull_WhenTheRealmSaysTheAccountIsGone()
    {
        _client.GetUserAsync(Ana.ToString(), Arg.Any<CancellationToken>()).Returns((KeycloakUserResponse?)null);
        Serve(Luis, "Luis", "Gómez");

        var names = await CreateSut().ResolveNamesAsync([Ana, Luis], CancellationToken.None);

        names[Ana].ShouldBeNull();
        names[Luis].ShouldBe("Luis Gómez");
    }

    [Fact]
    public async Task ResolveNamesAsync_ReturnsNull_WhenTheRealmFails()
    {
        _client.GetUserAsync(Ana.ToString(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("503"));
        Serve(Luis, "Luis", "Gómez");

        var names = await CreateSut().ResolveNamesAsync([Ana, Luis], CancellationToken.None);

        names[Ana].ShouldBeNull();
        names[Luis].ShouldBe("Luis Gómez");
    }

    [Fact]
    public async Task ResolveNamesAsync_ReturnsNull_WhenTheRealmAnswersAnUnreadableBody()
    {
        _client.GetUserAsync(Ana.ToString(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new System.Text.Json.JsonException("cuerpo ilegible"));

        var names = await CreateSut().ResolveNamesAsync([Ana], CancellationToken.None);

        names[Ana].ShouldBeNull();
    }

    [Fact]
    public async Task ResolveNamesAsync_ReturnsAnEmptyMap_WhenNobodyIsAsked()
    {
        var names = await CreateSut().ResolveNamesAsync([], CancellationToken.None);

        names.ShouldBeEmpty();
        await _client.DidNotReceiveWithAnyArgs().GetUserAsync(default!, default);
    }

    private void Serve(Guid id, string first, string last) =>
        _client.GetUserAsync(id.ToString(), Arg.Any<CancellationToken>())
            .Returns(new KeycloakUserResponse(id.ToString(), first.ToLowerInvariant(), null, first, last, Enabled: true));
}
