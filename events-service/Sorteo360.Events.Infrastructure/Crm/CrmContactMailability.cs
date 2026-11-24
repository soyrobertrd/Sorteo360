using System.Text.Json;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Infrastructure.Crm;

/// <summary>
/// La lista negra del sorteo, preguntada al CRM en vivo y sin caché: una copia dejaría pasar a
/// alguien vetado hace un minuto, para ahorrar llamadas que nadie midió (spec 01 §8).
/// <para>
/// Las tandas van de a mil, el tope del CRM, y en serie: en paralelo multiplicarían la carga sobre
/// un vecino del que el sorteo ya depende, por unos milisegundos. Es todo o nada (spec 01 §3.1).
/// </para>
/// </summary>
public sealed class CrmContactMailability : IContactMailability
{
    /// <summary>Cuántos ids admite el CRM en una sola consulta.</summary>
    private const int BatchSize = 1000;

    /// <summary>
    /// El nombre que se le da a un "no se puede invitar" que llega sin motivo. Es el código de la
    /// marca del CRM: quien decide es <c>canBeInvited</c>, y el motivo solo nombra (spec 01 §3).
    /// </summary>
    private const string UnnamedInviteSuppression = "do_not_invite";

    private readonly ICrmContactClient _client;
    private readonly ILogger<CrmContactMailability> _logger;

    public CrmContactMailability(ICrmContactClient client, ILogger<CrmContactMailability> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyDictionary<Guid, string?>, BaseError>> CheckAsync(
        IReadOnlyCollection<Guid> contactIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contactIds);

        var verdicts = new Dictionary<Guid, string?>(contactIds.Count);

        try
        {
            foreach (var batch in contactIds.Chunk(BatchSize))
            {
                var response = await _client.CheckMailabilityAsync(new CrmMailabilityRequest(batch), cancellationToken);

                if (response is null)
                {
                    // Un 200 sin cuerpo no es "nadie existe": tomarlo así dejaría a toda la tanda en
                    // contact_unresolved sin que nadie se entere de que el CRM falló.
                    _logger.LogError("El CRM devolvió un cuerpo vacío al consultar la contactabilidad de {Count} contactos.", batch.Length);
                    return Result.Failure<IReadOnlyDictionary<Guid, string?>, BaseError>(RaffleErrors.CrmUnavailable());
                }

                foreach (var verdict in response)
                {
                    if (verdict.CanBeInvited is not { } canBeInvited)
                    {
                        // Un CRM anterior a PGTI-190 no lo manda. Leerlo como false vaciaría el bombo
                        // en silencio; es una respuesta ilegible, y se informa como el CRM caído.
                        _logger.LogError("El CRM devolvió el contacto {ContactId} sin canBeInvited al consultar su contactabilidad.", verdict.Id);
                        return Result.Failure<IReadOnlyDictionary<Guid, string?>, BaseError>(RaffleErrors.CrmUnavailable());
                    }

                    verdicts[verdict.Id] = canBeInvited ? null : verdict.InviteSuppressionReason ?? UnnamedInviteSuppression;
                }
            }
        }
        catch (HttpRequestException exception)
        {
            // También llega por acá el circuito abierto, que el chasis convierte en esta excepción:
            // sin el catch, la falla que más se espera le llegaría al usuario como un 500.
            _logger.LogWarning(exception, "No se pudo consultar la contactabilidad de {Count} contactos en el CRM.", contactIds.Count);
            return Result.Failure<IReadOnlyDictionary<Guid, string?>, BaseError>(RaffleErrors.CrmUnavailable());
        }
        catch (JsonException exception)
        {
            // Un 200 con el cuerpo vacío de verdad, o ilegible, no llega como null sino así. Es el
            // mismo CRM que no dijo nada (spec 01 §3.1), y sin este catch saldría como un 500.
            _logger.LogError(exception, "El CRM devolvió un cuerpo ilegible al consultar la contactabilidad de {Count} contactos.", contactIds.Count);
            return Result.Failure<IReadOnlyDictionary<Guid, string?>, BaseError>(RaffleErrors.CrmUnavailable());
        }

        return Result.Success<IReadOnlyDictionary<Guid, string?>, BaseError>(verdicts);
    }
}
