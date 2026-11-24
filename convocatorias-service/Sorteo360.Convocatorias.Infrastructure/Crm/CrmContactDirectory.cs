using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Sorteo360.Convocatorias.Core.Crm;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Infrastructure.Neighbours;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Infrastructure.Crm;

/// <inheritdoc cref="ICrmContactDirectory" />
public sealed class CrmContactDirectory : ICrmContactDirectory
{
    /// <summary>
    /// Ids por llamada a <c>POST /v1/contacts/mailability</c>. Es el tope del CRM (spec <c>08</c>
    /// §10) y no una preferencia nuestra: pasarse es un 400, así que una campaña de 3.000
    /// destinatarios son tres llamadas y no una que rebota.
    /// </summary>
    internal const int MailabilityBatchSize = 1000;

    /// <summary>
    /// Ids por llamada a <c>GET /v1/contacts?id=</c>. Es el tope del CRM, que lo eligió para que
    /// la línea de la petición quede lejos del límite de Kestrel.
    /// </summary>
    internal const int IdFilterBatchSize = 100;

    private readonly ICrmContactsClient _client;
    private readonly ILogger<CrmContactDirectory> _logger;

    public CrmContactDirectory(ICrmContactsClient client, ILogger<CrmContactDirectory> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CrmContact>, BaseError>> ListContactsAsync(
        IReadOnlyList<string> classifications,
        IReadOnlyList<string> companyCategories,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(classifications);
        ArgumentNullException.ThrowIfNull(companyCategories);

        // El paginado, el tope y la traducción de la avería son del lector compartido; acá solo
        // queda qué se le pide al CRM y en qué se convierte cada ficha.
        var contacts = await NeighbourPageReader.ReadEveryPageAsync(
            fetchPageAsync: (pageNumber, pageSize, token) => _client.ListContactsAsync(
                classifications,
                companyCategories,
                pageNumber,
                pageSize,
                token),
            itemsOf: response => response.Items,
            unavailable: new CrmUnavailableError(),
            logger: _logger,
            neighbour: "El CRM",
            listing: "los destinatarios de una selección",
            cancellationToken: cancellationToken);

        return contacts.Map(items => (IReadOnlyList<CrmContact>)items.Select(ToContact).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CrmContact>, BaseError>> ListContactsByIdAsync(
        IReadOnlyList<Guid> contactIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contactIds);

        var contacts = new List<CrmContact>();

        // Las tandas van en serie, como las de la recomprobación, y la primera que falla hace
        // fallar la selección entera: media lista no se escribe.
        foreach (var batch in contactIds.Distinct().Chunk(IdFilterBatchSize))
        {
            var page = await NeighbourPageReader.ReadEveryPageAsync(
                fetchPageAsync: (pageNumber, pageSize, token) =>
                    _client.ListContactsByIdAsync(batch, pageNumber, pageSize, token),
                itemsOf: response => response.Items,
                unavailable: new CrmUnavailableError(),
                logger: _logger,
                neighbour: "El CRM",
                listing: "los contactos elegidos de una selección",
                cancellationToken: cancellationToken);

            if (page.IsFailure)
                return Result.Failure<IReadOnlyList<CrmContact>, BaseError>(page.Error);

            contacts.AddRange(page.Value.Select(ToContact));
        }

        return contacts;
    }

    /// <inheritdoc />
    public Task<Result<Maybe<CrmContactList>, BaseError>> FindContactListAsync(
        Guid contactListId,
        CancellationToken cancellationToken) =>
        FindByIdAsync(
            token => _client.GetContactListAsync(contactListId, token),
            list => new CrmContactList(list.Id, list.OptOfficeId, list.ContactIds ?? []),
            "leer una lista de contactos",
            cancellationToken);

    /// <summary>
    /// Una lectura por id: el 404 es <see cref="Maybe{T}.None"/>, y un cuerpo vacío o una avería
    /// son <c>503 CRM_UNAVAILABLE</c>.
    /// </summary>
    private async Task<Result<Maybe<TDomain>, BaseError>> FindByIdAsync<TResponse, TDomain>(
        Func<CancellationToken, Task<CrmByIdRead<TResponse>>> readAsync,
        Func<TResponse, TDomain> toDomain,
        string operation,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        return await NeighbourCall.GuardAsync(
            async () =>
            {
                var read = await readAsync(cancellationToken);

                if (!read.Found)
                    return Result.Success<Maybe<TDomain>, BaseError>(Maybe<TDomain>.None);

                if (read.Body is null)
                {
                    _logger.LogError("El CRM devolvió un cuerpo vacío al {Operation}", operation);
                    return Result.Failure<Maybe<TDomain>, BaseError>(new CrmUnavailableError());
                }

                return Result.Success<Maybe<TDomain>, BaseError>(Maybe.From(toDomain(read.Body)));
            },
            new CrmUnavailableError(),
            _logger,
            "El CRM",
            operation,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<Maybe<CrmContact>, BaseError>> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return await NeighbourCall.GuardAsync(
            async () =>
            {
                var response = await _client.FindContactByEmailAsync(email.Trim(), cancellationToken);

                if (response is null)
                {
                    // Un 200 con el cuerpo vacío es un CRM que contestó sin decir nada. No es "no
                    // está" —eso viene con `items: []`— así que informar la fila como nueva la
                    // crearía por duplicado en el CRM.
                    _logger.LogError("El CRM devolvió un cuerpo vacío al buscar un contacto por correo");
                    return Result.Failure<Maybe<CrmContact>, BaseError>(new CrmUnavailableError());
                }

                var contact = response.Items.FirstOrDefault();

                return Result.Success<Maybe<CrmContact>, BaseError>(
                    contact is null ? Maybe<CrmContact>.None : Maybe.From(ToContact(contact)));
            },
            new CrmUnavailableError(),
            _logger,
            "El CRM",
            "buscar un contacto por correo",
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<CrmContactCreation, BaseError>> CreateContactAsync(
        NewCrmContact contact,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contact);

        return await NeighbourCall.GuardAsync(
            async () =>
            {
                var response = await _client.CreateContactAsync(
                    new CrmCreateContactRequest(
                        contact.FirstName,
                        contact.LastName,
                        contact.Email,
                        // Un teléfono que el CRM rechazaría no viaja: su 400 se leería como una
                        // caída y el alta fallaría en cada reintento. La inscripción o la fila de
                        // la carga siguen guardando el teléfono tal como llegó.
                        PhoneShape.IsAcceptedByCrm(contact.Phone) ? contact.Phone : null,
                        HasConsent: contact.ConsentSource is not null,
                        contact.ConsentSource),
                    cancellationToken);

                // Ya existía. Se toma su ficha entera, no solo su id, porque quien llama tiene que
                // volver a mirar su `canBeEmailed`: el contacto detrás de un 409 puede estar de baja.
                if (response.ExistingContact is { } existing)
                {
                    return Result.Success<CrmContactCreation, BaseError>(
                        new CrmContactCreation(ToContact(existing), AlreadyExisted: true));
                }

                if (response.CreatedId is not { } createdId || createdId == Guid.Empty)
                {
                    _logger.LogError("El CRM aceptó un alta de contacto y no devolvió su identidad");
                    return Result.Failure<CrmContactCreation, BaseError>(new CrmUnavailableError());
                }

                // Recién creado: el CRM devuelve solo el id, así que el resto de la ficha es lo que
                // acabamos de mandar. Se le puede escribir por construcción —trae dirección y no
                // tiene ninguna supresión, que es lo único que apaga `canBeEmailed`.
                return Result.Success<CrmContactCreation, BaseError>(new CrmContactCreation(
                    new CrmContact(createdId, contact.FirstName, contact.LastName, contact.Email, CanBeEmailed: true, null),
                    AlreadyExisted: false));
            },
            new CrmUnavailableError(),
            _logger,
            "El CRM",
            "crear un contacto",
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UnitResult<BaseError>> OptOutAsync(Guid contactId, CancellationToken cancellationToken)
    {
        return await NeighbourCall.GuardAsync(
            async () =>
            {
                var outcome = await _client.OptOutContactAsync(
                    contactId,
                    new CrmOptOutRequest(CrmOptOutSources.CampaignUnsubscribe),
                    cancellationToken);

                // El CRM ya no tiene esa ficha. Lo que se le pedía —que sepa que esa persona se dio
                // de baja— se cumple igual, porque a una ficha borrada no le escribe nadie, así que
                // no es un fallo y la baja queda por sellada. Fallar acá dejaría esa fila
                // reintentándose en cada lanzamiento del servicio, y ninguno volvería a pasar del
                // primer paso.
                if (outcome == CrmOptOutOutcome.ContactGone)
                {
                    _logger.LogWarning(
                        "El CRM ya no tiene el contacto {ContactId} al empujarle su baja de campaña",
                        contactId);
                }

                return UnitResult.Success<BaseError>();
            },
            new CrmUnavailableError(),
            _logger,
            "El CRM",
            "empujarle una baja de campaña",
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CrmMailability>, BaseError>> CheckMailabilityAsync(
        IReadOnlyList<Guid> contactIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contactIds);

        if (contactIds.Count == 0)
            return Result.Success<IReadOnlyList<CrmMailability>, BaseError>([]);

        return await NeighbourCall.GuardAsync(
            async () =>
            {
                var verdicts = new List<CrmMailability>(contactIds.Count);

                // El tope es del CRM, así que partir la lista es de este lado. Las tandas van en
                // serie: son parte de la petición de lanzamiento y adelantar el paralelismo acá
                // multiplicaría por cuatro la carga sobre el vecino del que ya dependemos.
                foreach (var batch in contactIds.Chunk(MailabilityBatchSize))
                {
                    var response = await _client.CheckMailabilityAsync(
                        new CrmMailabilityRequest(batch),
                        cancellationToken);

                    if (response is null)
                    {
                        // Un 200 con el cuerpo vacío no es "ninguno de estos se puede contactar"
                        // —eso viene con una lista vacía de veredictos, y tiene su propio desenlace—
                        // sino un CRM que contestó sin decir nada. Tomarlo por respuesta suprimiría
                        // la campaña entera en silencio.
                        _logger.LogError("El CRM devolvió un cuerpo vacío al recomprobar la contactabilidad");
                        return Result.Failure<IReadOnlyList<CrmMailability>, BaseError>(new CrmUnavailableError());
                    }

                    verdicts.AddRange(response.Select(item =>
                        new CrmMailability(item.Id, item.CanBeEmailed, item.SuppressionReason)));
                }

                return Result.Success<IReadOnlyList<CrmMailability>, BaseError>(verdicts);
            },
            new CrmUnavailableError(),
            _logger,
            "El CRM",
            "recomprobar la contactabilidad",
            cancellationToken);
    }

    /// <summary>
    /// El nombre y los apellidos nunca son nulos en la fila que se escribe, así que un CRM que
    /// los omita se resuelve acá con un texto y no reventando la selección entera: la ficha
    /// existe, se le puede escribir, y quedarse sin nombre no es motivo para no convocarla.
    /// </summary>
    private static CrmContact ToContact(CrmContactResponse response) =>
        new(
            response.Id,
            string.IsNullOrWhiteSpace(response.FirstName) ? "(sin nombre)" : response.FirstName,
            string.IsNullOrWhiteSpace(response.LastName) ? "(sin apellido)" : response.LastName,
            response.Email,
            response.CanBeEmailed,
            response.SuppressionReason);
}
