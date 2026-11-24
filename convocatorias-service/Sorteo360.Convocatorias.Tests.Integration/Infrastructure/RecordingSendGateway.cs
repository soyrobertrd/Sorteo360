using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Tests.Integration.Infrastructure;

/// <summary>
/// El transporte visto desde el worker: acepta lo que le llega y apunta con qué se le llamó.
/// <para>
/// Es un doble de la costura, no del transporte, y sigue existiendo teniéndolo: el
/// <c>StubCampaignSendGateway</c> escribe en su tabla y acepta a todo el mundo, así que no sirve
/// para colgar la entrega ni para mirar con qué token se llamó. Lo que el stub de verdad escribe
/// se cobra aparte, en <c>StubCampaignSendGatewayTests</c>.
/// </para>
/// </summary>
public sealed class RecordingSendGateway : ICampaignSendGateway
{
    /// <summary>El único motivo de rechazo que el contrato admite al aceptar (spec <c>04</c> §2.1).</summary>
    public const string RejectionReason = "RECIPIENT_ADDRESS_INVALID";

    private readonly List<SendBatchRequest> _requests = [];
    private readonly List<CancellationToken> _tokens = [];
    private readonly Lock _guard = new();

    private TaskCompletionSource? _gate;

    private int _callsToFail;
    private readonly HashSet<Guid> _rejected = [];

    /// <summary>Los lotes que llegaron, en orden.</summary>
    public IReadOnlyList<SendBatchRequest> Requests
    {
        get
        {
            lock (_guard)
                return [.. _requests];
        }
    }

    /// <summary>
    /// Los tokens con los que se llamó. <b>Es la prueba del ticket</b>: si alguno viene de un
    /// manejador, se cancela al terminar la petición y el envío muere con la pestaña.
    /// </summary>
    public IReadOnlyList<CancellationToken> Tokens
    {
        get
        {
            lock (_guard)
                return [.. _tokens];
        }
    }

    /// <summary>
    /// Deja el transporte colgado hasta que alguien llame a <see cref="Release"/>. Es cómo un test
    /// se asegura de que el <c>202</c> ya se leyó antes de que salga un solo lote.
    /// </summary>
    public void Hold()
    {
        lock (_guard)
            _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Olvida lo apuntado y suelta lo que estuviera colgado. Los tests de una clase comparten un
    /// host, así que sin esto cada uno vería además los lotes del anterior.
    /// </summary>
    public void Reset()
    {
        Release();

        lock (_guard)
        {
            _requests.Clear();
            _tokens.Clear();
            _rejected.Clear();
            _callsToFail = 0;
        }
    }

    /// <summary>
    /// Las próximas <paramref name="count"/> llamadas se caen enteras, como una avería de
    /// transporte: sin anotar a nadie y dejando las filas <c>pending</c>. Es lo que deja probar la
    /// reanudación, que es lo que una persona hace después (spec <c>04</c> §5).
    /// </summary>
    public void FailNextCalls(int count)
    {
        lock (_guard)
            _callsToFail = count;
    }

    /// <summary>
    /// Esta fila vuelve como rechazada dentro de un lote aceptado. Es terminal para esa persona, y
    /// es lo que <c>retry-failed</c> deshace.
    /// </summary>
    public void Reject(Guid recipientId)
    {
        lock (_guard)
            _rejected.Add(recipientId);
    }

    /// <summary>Deja de rechazar a nadie, para que la vuelta siguiente sí salga.</summary>
    public void AcceptEveryone()
    {
        lock (_guard)
            _rejected.Clear();
    }

    /// <summary>Suelta lo que estuviera esperando en <see cref="Hold"/>.</summary>
    public void Release()
    {
        lock (_guard)
        {
            _gate?.TrySetResult();
            _gate = null;
        }
    }

    /// <inheritdoc />
    public async Task<Result<SendBatchResult, BaseError>> SendAsync(
        SendBatchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        TaskCompletionSource? gate;
        bool fails;
        Guid[] rejected;

        lock (_guard)
        {
            _requests.Add(request);
            _tokens.Add(cancellationToken);
            gate = _gate;

            fails = _callsToFail > 0;

            if (fails)
                _callsToFail--;

            rejected = [.. _rejected];
        }

        if (gate is not null)
            await gate.Task;

        if (fails)
        {
            return Result.Failure<SendBatchResult, BaseError>(
                new NotificationsUnavailableError("no respondió"));
        }

        return Result.Success<SendBatchResult, BaseError>(
            new SendBatchResult([
                .. request.Recipients.Select(recipient => rejected.Contains(recipient.RecipientId)
                    ? RecipientHandoffOutcome.Rejected(recipient.RecipientId, RejectionReason)
                    : RecipientHandoffOutcome.Accepted(recipient.RecipientId)),
            ]));
    }
}
