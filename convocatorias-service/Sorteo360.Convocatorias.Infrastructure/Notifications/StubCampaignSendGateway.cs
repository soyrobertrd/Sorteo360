using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Rendering;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Infrastructure.Notifications;

/// <summary>
/// El transporte mientras notification-service no existe: sustituye el correo de cada persona y
/// lo guarda en <c>stub_notification</c>, en la base de este servicio (spec <c>04</c> §4.1).
/// <para>
/// <b>Es una implementación real, no un doble.</b> Es el lado que sustituye, porque es el lado que
/// sustituye en la spec <c>04</c> §2, y el motor que usa es el mismo
/// <see cref="ISubstitutionEngine"/> que ya vigila la copia sembrada: los dos lados hablan el
/// mismo dialecto plano, literal y escapado a propósito. Con él, el servicio hace su trabajo
/// principal de punta a punta sin depender de que notification-service exista ni de que el cliente
/// haya elegido proveedor.
/// </para>
/// <para>
/// <b>El único rechazo que existe al aceptar es una dirección mal formada</b> (§2.1). No hay más
/// desenlaces posibles de este lado: lo que el proveedor diga después llega por un webhook que
/// nadie pidió todavía.
/// </para>
/// <para>
/// <b>Su escritura y la del desenlace son dos guardados distintos, sin transacción común.</b> Es a
/// conciencia: el <c>202</c> es la línea y después de él la fila del historial tiene que existir
/// (§2.1), así que guardar antes de contestar es lo correcto aunque el guardado de la campaña
/// falle después. Lo que se acepta es que una reanudación vuelva a escribir la fila de esa persona
/// en esta tabla, porque no tiene id de campaña sobre el que deduplicar —también a propósito,
/// §4.1—. Se sobrecuenta una tabla temporal; las filas de la campaña siguen exactas, que es lo
/// que la cola sin entregar de §5 garantiza.
/// </para>
/// <para>
/// <b>La fila de un rechazo también guarda su cuerpo, y con él un token en claro.</b> No amplía lo
/// que §4.2 acepta: de una fila que termina <c>failed</c> no se guarda ningún hash (spec <c>06</c>
/// §2.1), así que ese token no abre nada. Es una credencial muerta desde el momento en que se
/// escribe.
/// </para>
/// </summary>
public sealed class StubCampaignSendGateway : ICampaignSendGateway
{
    private readonly ConvocatoriasDbContext _context;
    private readonly ISubstitutionEngine _engine;

    public StubCampaignSendGateway(ConvocatoriasDbContext context, ISubstitutionEngine engine)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(engine);

        _context = context;
        _engine = engine;
    }

    /// <inheritdoc />
    public async Task<Result<SendBatchResult, BaseError>> SendAsync(
        SendBatchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Las dos reglas del receptor, antes de escribir nada: el tope de mil y la clave repetida
        // (spec 04 §2 y §2.2). Las corre el stub igual que el cliente real, o stage aceptaría un
        // lote que producción rechaza, que es justo lo que el transporte como valor existe para
        // impedir.
        var contract = NotificationBatchContract.Validate(request);

        if (contract.IsFailure)
            return Result.Failure<SendBatchResult, BaseError>(contract.Error);

        // Un lote, un id. Es lo que devolvería la respuesta como `batchId` y lo que agrupa las
        // filas de una misma llamada.
        var batchId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var outcomes = new List<RecipientHandoffOutcome>(request.Recipients.Count);

        foreach (var recipient in request.Recipients)
        {
            var rendered = _engine.Render(request.Subject, request.BodyHtml, ValuesFor(request, recipient));

            // La dirección se copió al seleccionarla y viene del CRM, así que en la práctica esto
            // no rechaza a nadie. Existe porque es el desenlace que el contrato admite, y sin él
            // `failed` sería una rama que nunca corre hasta el día que corra en producción.
            var accepted = EmailAddressShape.IsWellFormed(recipient.Email);

            // Se guarda también la fila rechazada. La respuesta del servicio real no le da id a un
            // rechazo, pero esta tabla tiene columna de estado justamente para que se vea a quién
            // no se le escribió y por qué.
            _context.StubNotifications.Add(new StubNotificationData
            {
                Id = Guid.NewGuid(),
                BatchId = batchId,
                RecipientEmail = recipient.Email,
                Type = CampaignNotification.Type,
                Language = request.Language,
                Subject = rendered.Subject,
                BodyHtml = rendered.BodyHtml,
                Status = accepted ? CampaignNotification.StatusAccepted : CampaignNotification.StatusRejected,
                FailureReason = accepted ? null : CampaignNotification.ReasonAddressInvalid,
                CreatedAt = now,
            });

            outcomes.Add(accepted
                ? RecipientHandoffOutcome.Accepted(recipient.RecipientId)
                : RecipientHandoffOutcome.Rejected(recipient.RecipientId, CampaignNotification.ReasonAddressInvalid));
        }

        // Una sola escritura por lote, y antes de contestar: el `202` es la línea, y después de
        // él tiene que existir la fila (§2.1). Si esto se cae, el lote entero falla y sus filas de
        // campaña se quedan `pending`, que es lo que permite reanudar.
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success<SendBatchResult, BaseError>(new SendBatchResult(outcomes));
    }

    /// <summary>
    /// Las dos bolsas en una sola, que es lo que sustituye.
    /// <para>
    /// <b>Se añade, no se pisa.</b> Una clave en las dos bolsas nunca es una regla de precedencia
    /// (§2): un ganador silencioso es un generador de fallos. Acá no puede haber colisión porque
    /// <see cref="NotificationBatchContract.Validate"/> ya la rechazó con su código; el
    /// <c>Add</c> se queda porque es lo que hace que esa garantía se note si algún día alguien
    /// mueve la guarda.
    /// </para>
    /// </summary>
    private static Dictionary<string, string> ValuesFor(SendBatchRequest request, SendBatchRecipient recipient)
    {
        var values = new Dictionary<string, string>(request.SharedData, StringComparer.Ordinal);

        foreach (var (key, value) in recipient.Data)
            values.Add(key, value);

        return values;
    }
}
