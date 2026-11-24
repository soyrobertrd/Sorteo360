using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sorteo360.Events.Infrastructure.Directories;
using Sorteo360.Events.Tests.Unit.Shared;
using NSubstitute;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Directories;

/// <summary>
/// La máquina que comparten los cuatro directorios.
/// <para>
/// <b>La regla que se prueba acá y en ningún otro lado</b> es que una copia sobrevive a su propia
/// vigencia: el TTL decide cuándo hay que renovar, no cuándo se destruye lo guardado. La
/// implementación obvia —poner el TTL como expiración de la entrada— borra la última copia buena
/// justo en el momento en que el origen caído la hace necesaria.
/// </para>
/// </summary>
public class StaleTolerantCacheTests
{
    private const string Key = "directory:thing";

    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    private readonly TestTimeProvider _time = new();
    private readonly IDistributedCache _cache =
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private StaleTolerantCache CreateSut() =>
        new(_cache, _time, NullLogger.Instance);

    #region La vigencia decide cuándo renovar

    [Fact]
    public async Task ReadOrRefreshAsync_ReturnsTheCopyWithoutAskingTheOrigin_WhenItIsFresh()
    {
        var sut = CreateSut();
        var origin = new CountingOrigin(new Thing("original"));

        await Read(sut, origin);
        var second = await Read(sut, origin);

        origin.Calls.ShouldBe(1);
        second.Outcome.ShouldBe(CacheOutcome.Fresh);
        second.Value.ShouldNotBeNull().Name.ShouldBe("original");
    }

    [Fact]
    public async Task ReadOrRefreshAsync_ReturnsTheRefreshedValue_WhenTheCopyIsStale()
    {
        var sut = CreateSut();
        var origin = new CountingOrigin(new Thing("original"));

        await Read(sut, origin);

        origin.Answer = new Thing("renovado");
        _time.Advance(Ttl + TimeSpan.FromMinutes(1));

        var refreshed = await Read(sut, origin);

        origin.Calls.ShouldBe(2);
        refreshed.Outcome.ShouldBe(CacheOutcome.Fresh);
        refreshed.Value.ShouldNotBeNull().Name.ShouldBe("renovado");
    }

    [Fact]
    public async Task ReadOrRefreshAsync_ReturnsTheOriginAnswer_WhenTheExtraCheckTurnsDownAFreshCopy()
    {
        // La comprobación extra es la del directorio de aprobadores: una copia puede estar
        // vigente y aun así no servir, porque se armó preguntando por otro conjunto de roles.
        var sut = CreateSut();
        var origin = new CountingOrigin(new Thing("original"));

        await Read(sut, origin);
        await Read(sut, origin, stillApplies: _ => false);

        origin.Calls.ShouldBe(2);
    }

    #endregion

    #region Una copia vencida sigue existiendo

    [Fact]
    public async Task ReadOrRefreshAsync_ReturnsTheExpiredCopy_WhenTheRefreshFails()
    {
        var sut = CreateSut();
        var origin = new CountingOrigin(new Thing("original"));

        await Read(sut, origin);

        origin.Fails = new HttpRequestException("origen caído");
        _time.Advance(Ttl * 100);

        var served = await Read(sut, origin);

        served.Outcome.ShouldBe(CacheOutcome.Stale);
        served.Value.ShouldNotBeNull().Name.ShouldBe("original");
    }

    [Fact]
    public async Task ReadOrRefreshAsync_ReturnsMissing_WhenTheRefreshFailsAndThereNeverWasACopy()
    {
        var sut = CreateSut();
        var origin = new CountingOrigin(null) { Fails = new HttpRequestException("origen caído") };

        var read = await Read(sut, origin);

        read.Outcome.ShouldBe(CacheOutcome.Missing);
        read.Value.ShouldBeNull();
    }

    [Fact]
    public async Task ReadOrRefreshAsync_ReturnsTheFailureToTheCallersLogger_WhenTheRefreshFails()
    {
        var sut = CreateSut();
        var origin = new CountingOrigin(null) { Fails = new HttpRequestException("origen caído") };
        Exception? logged = null;

        await sut.ReadOrRefreshAsync(Key, Policy(), origin.FetchAsync, exception => logged = exception, CancellationToken.None);

        logged.ShouldBeOfType<HttpRequestException>();
    }

    #endregion

    #region Una ausencia guardada no es una entrada que falte

    [Fact]
    public async Task ReadOrRefreshAsync_ReturnsTheCachedAbsence_WhenThePolicyCachesAbsence()
    {
        // Quien se fue del realm resuelve a nada, y esa nada se guarda: volver a preguntarla en
        // cada decisión es una llamada por un correo que no se va a mandar igual.
        var sut = CreateSut();
        var origin = new CountingOrigin(null);

        await Read(sut, origin, Policy(cachesAbsence: true));
        var second = await Read(sut, origin, Policy(cachesAbsence: true));

        origin.Calls.ShouldBe(1);
        second.Outcome.ShouldBe(CacheOutcome.Fresh);
        second.Value.ShouldBeNull();
    }

    [Fact]
    public async Task ReadOrRefreshAsync_ReturnsTheOriginAnswerAgain_WhenThePolicyDoesNotCacheAnAbsence()
    {
        // La cola larga del CRM no guarda la ausencia: una oficina que hoy no está puede aparecer
        // mañana, y nadie paga por preguntarla porque casi siempre está.
        var sut = CreateSut();
        var origin = new CountingOrigin(null);

        await Read(sut, origin);
        await Read(sut, origin);

        origin.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task ReadOrRefreshAsync_ReturnsTheOriginAnswer_WhenTheExtraCheckTurnsDownACachedAbsence()
    {
        // La trampa que esta clase existe para cerrar: si la máquina diera por buena una ausencia
        // sin preguntar, una copia que guarda "no hay nadie" y además tiene condición extra se
        // serviría toda su vigencia aunque la condición ya no se cumpliera.
        var sut = CreateSut();
        var origin = new CountingOrigin(null);

        await Read(sut, origin, Policy(cachesAbsence: true));
        await Read(sut, origin, Policy(cachesAbsence: true), stillApplies: value => value is not null);

        origin.Calls.ShouldBe(2);
    }

    #endregion

    #region La entrada que no se puede leer

    [Fact]
    public async Task ReadOrRefreshAsync_ReturnsTheOriginAnswer_WhenTheCachedEntryIsUnreadable()
    {
        await _cache.SetAsync(Key, "esto no es la copia"u8.ToArray(), CancellationToken.None);

        var origin = new CountingOrigin(new Thing("original"));

        var read = await Read(CreateSut(), origin);

        read.Outcome.ShouldBe(CacheOutcome.Fresh);
        read.Value.ShouldNotBeNull().Name.ShouldBe("original");
    }

    [Fact]
    public async Task ReadOrRefreshAsync_ReturnsMissing_WhenTheResponseIsUnreadableAndThePolicySwallowsIt()
    {
        // Un 200 de Keycloak con un cuerpo ilegible es el mismo problema que Keycloak caído.
        var sut = CreateSut();
        var origin = new CountingOrigin(null) { Fails = new JsonException("cuerpo ilegible") };

        var read = await Read(sut, origin, Policy(swallowsUnreadableResponses: true));

        read.Outcome.ShouldBe(CacheOutcome.Missing);
    }

    [Fact]
    public async Task ReadOrRefreshAsync_ThrowsJsonException_WhenTheResponseIsUnreadableAndThePolicyDoesNotSwallowIt()
    {
        // Los directorios del CRM no la tragan, y ensancharlo convertiría un 500 en una lectura
        // silenciosamente vieja.
        var sut = CreateSut();
        var origin = new CountingOrigin(null) { Fails = new JsonException("cuerpo ilegible") };

        await Should.ThrowAsync<JsonException>(() => Read(sut, origin));
    }

    #endregion

    #region El cerrojo, que no todas las copias tienen

    [Fact]
    public async Task ReadOrRefreshAsync_ReturnsOnlyAfterTheLockIsReleased_WhenThePolicyCarriesALock()
    {
        var sut = CreateSut();
        var refreshLock = new TestRefreshLock();
        var origin = new CountingOrigin(new Thing("original"));

        await refreshLock.WaitAsync(CancellationToken.None);

        var read = Read(sut, origin, Policy(refreshLock: refreshLock));
        read.IsCompleted.ShouldBeFalse();

        refreshLock.Release();

        (await read).Value.ShouldNotBeNull().Name.ShouldBe("original");
    }

    [Fact]
    public async Task ReadOrRefreshAsync_ReturnsTheOriginAnswer_WhenThePolicyCarriesNoLock()
    {
        // Los rellenos por id no toman cerrojo: son la cola larga del directorio y no compiten
        // con la renovación de la copia completa.
        var sut = CreateSut();
        var origin = new CountingOrigin(new Thing("original"));

        var read = Read(sut, origin);

        (await read).Value.ShouldNotBeNull().Name.ShouldBe("original");
    }

    #endregion

    #region La retención, que no es la vigencia

    [Fact]
    public async Task ReadOrRefreshAsync_ReturnsTheOriginValueWrittenForTheRetention_WhenTheCopyIsWritten()
    {
        var cache = Substitute.For<IDistributedCache>();
        cache.GetAsync(Key, Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        var sut = new StaleTolerantCache(cache, _time, NullLogger.Instance);
        var origin = new CountingOrigin(new Thing("original"));

        var read = await Read(sut, origin);

        read.Value.ShouldNotBeNull().Name.ShouldBe("original");
        await cache.Received(1).SetAsync(
            Key,
            Arg.Any<byte[]>(),
            Arg.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == Retention),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Montaje

    private static CacheCopyPolicy Policy(
        DirectoryRefreshLock? refreshLock = null,
        bool swallowsUnreadableResponses = false,
        bool cachesAbsence = false) =>
        new(Ttl, Retention, refreshLock, swallowsUnreadableResponses, cachesAbsence);

    private static Task<CacheLookup<Thing>> Read(
        StaleTolerantCache sut,
        CountingOrigin origin,
        CacheCopyPolicy? policy = null,
        Func<Thing?, bool>? stillApplies = null) =>
        sut.ReadOrRefreshAsync(
            Key,
            policy ?? Policy(),
            stillApplies,
            origin.FetchAsync,
            _ => { },
            CancellationToken.None);

    /// <summary>Un cerrojo que no es de ningún directorio, como la máquina que protege.</summary>
    private sealed class TestRefreshLock : DirectoryRefreshLock;

    /// <summary>Lo que el origen contesta, y cuántas veces se lo preguntaron.</summary>
    private sealed class CountingOrigin
    {
        public CountingOrigin(Thing? answer) => Answer = answer;

        public Thing? Answer { get; set; }

        public Exception? Fails { get; set; }

        public int Calls { get; private set; }

        public Task<Thing?> FetchAsync(CancellationToken cancellationToken)
        {
            Calls++;

            return Fails is not null ? Task.FromException<Thing?>(Fails) : Task.FromResult(Answer);
        }
    }

    /// <summary>Algo guardable, que es todo lo que la máquina necesita saber de lo que guarda.</summary>
    public sealed record Thing(string Name);

    #endregion
}
