using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using WireMock;
using WireMock.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Types;
using WireMock.Util;

namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>
/// El CRM visto desde events-service, sustituido por un servidor real que habla HTTP.
/// <para>
/// Se sustituye el servicio y no el cliente tipado a propósito: lo que hay que ejercitar es la
/// cadena entera —el cliente, la tubería de resiliencia del chasis, la caché y la degradación—, y
/// nada de eso corre si se reemplaza <c>ICrmOptClient</c> por un doble.
/// </para>
/// <para>
/// <b>Cuenta lo del test que corre, y nada más.</b> WireMock apunta una petición en su registro
/// cuando termina de contestarla, no cuando la recibe, así que la que un vecino lento contesta
/// tarde aterriza en el registro ya limpio del test siguiente y pasa por suya —la fuga de
/// PGTI-32, que costó un fallo intermitente cada tres corridas en <c>NotificationStub</c>—. Acá
/// ninguna regla tiene retraso, así que hoy no hay nada que se filtre; el corte está igual que
/// allá para que ponerlo no dependa de que alguien recuerde esto. El mecanismo y su prueba viven
/// en <c>NotificationStubTests</c>.
/// </para>
/// <para>
/// Sirve las familias de rutas que este servicio consume: el directorio de OPT, las empresas, la
/// contactabilidad que el sorteo usa como lista negra y los contactos por id con que se nombra al
/// patrocinador de un premio. Se programan por separado y se apagan por
/// separado, porque un test que tumba una no está diciendo nada de las otras.
/// </para>
/// </summary>
public sealed class CrmDirectoryStub : IDisposable
{
    private const string OfficesPath = "/v1/opts";
    private const string CompaniesPath = "/v1/companies";
    private const string CompanyDirectoryPath = "/v1/companies/directory";
    private const string MailabilityPath = "/v1/contacts/mailability";
    private const string ContactsPath = "/v1/contacts";

    /// <summary>La oficina que usan por omisión los tests que no van sobre el directorio.</summary>
    public static readonly Guid OfficeId = Guid.Parse("0a5e1b8c-4c1e-4f6b-9c2a-6d7e8f901234");

    /// <summary>Una persona vigente de esa oficina.</summary>
    public static readonly Guid ContactId = Guid.Parse("1b6f2c9d-5d2f-4a7c-8d3b-7e8f90123456");

    public const string OfficeName = "OPT Madrid";

    /// <summary>El <c>opt_code</c> de esa oficina: el puente hasta quien puede aprobar.</summary>
    public const string OfficeCode = "madrid";
    public const string ContactName = "Ana Pérez";
    public const string CompanyName = "Hotel Jaragua";
    public const string CompanyCountry = "República Dominicana";
    public const string CompanyCity = "Santo Domingo";

    private readonly WireMockServer _server = WireMockServer.Start();

    /// <summary>
    /// El corte: desde cuándo cuenta lo que llegue. Lo mueve cada limpieza del registro, y con él
    /// deja fuera lo que entró antes aunque se apunte después.
    /// </summary>
    private DateTime _countingFrom = DateTime.UtcNow;

    /// <summary>
    /// Lo último que se pidió servir de empresas, o <c>null</c> mientras cualquier id resuelva. Se
    /// guarda porque reprogramar el directorio de OPT borra todas las rutas del servidor, y sin esto
    /// un test que reprograma oficinas apagaría las empresas de rebote: el CRM real no funciona así,
    /// y el fallo aparecería lejos de su causa.
    /// </summary>
    private StubCrmCompany[]? _companies;

    /// <summary>
    /// Los contactos publicados por empresa. Viven aparte de <see cref="_companies"/> porque el CRM
    /// los sirve en su propia ruta y no dentro de la empresa: el volcado del directorio no los trae
    /// a propósito, y la ficha del proveedor es la única pantalla que los pide (ADR-0015).
    /// </summary>
    private readonly Dictionary<Guid, StubCrmContact[]> _contacts = [];

    /// <summary>
    /// Las cuentas bancarias publicadas por empresa. Como los contactos, el CRM las sirve en su propia
    /// ruta, y una cuenta que no se publicó contesta 404, igual que la de otra empresa (ADR-0028).
    /// </summary>
    private readonly Dictionary<Guid, StubBankAccount[]> _bankAccounts = [];

    /// <summary>
    /// La regla que apaga las empresas, si está puesta. Se identifica para poder quitarla sin
    /// reiniciar el servidor entero: reiniciarlo se llevaría también el directorio de OPT, que es
    /// justo lo que esta clase existe para no hacer.
    /// </summary>
    private static readonly Guid CompaniesOutageMappingId = Guid.Parse("9d8c7b6a-5e4f-4321-8765-0fedcba98765");

    /// <summary>
    /// Las reglas de empresa puestas por el <c>Serve*</c> anterior. Se borran antes de poner las
    /// nuevas para que "estas empresas y solo estas" sea cierto: sin esto, una empresa publicada por
    /// un test seguiría resolviendo en el siguiente, y el caso que más importa —una ficha cuya
    /// empresa ya no resuelve— sería imposible de montar.
    /// </summary>
    private readonly List<Guid> _companyMappingIds = [];

    /// <summary>
    /// Lo declarado para cada contacto: el motivo del correo y el de la invitación, por separado,
    /// como los contesta el CRM (spec 03 §1a, §1b). Un contacto sin declarar contesta que se le
    /// puede escribir e invitar, para que los tests que no van sobre la lista negra no tengan que
    /// declarar a nadie. Se lee en cada petición, así que cambiarlo no exige reprogramar la ruta.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, StubMailability> _verdicts = new();

    /// <summary>Los contactos de los que el CRM no tiene fila: no vienen en la respuesta.</summary>
    private readonly ConcurrentDictionary<Guid, bool> _unknownContacts = new();

    private volatile bool _mailabilityDown;

    private volatile bool _contactsDown;

    private static readonly Guid ContactsByIdMappingId = Guid.Parse("6e5d4c3b-2a19-4807-b6f5-e4d3c2b1a090");

    private static readonly Guid MailabilityMappingId = Guid.Parse("4c3b2a19-0807-4f6e-9d5c-b4a392817160");

    public CrmDirectoryStub()
    {
        ServeAnyCompany();
        ServeDefaultDirectory();
    }

    /// <summary>URL base del CRM simulado, tal como la recibe <c>Services__Crm__BaseUrl</c>.</summary>
    public string Url => _server.Url!;

    /// <summary>
    /// Las peticiones que llegaron después del último corte, que son las de este test. Lo que un
    /// vecino lento contestara tarde conservaría su hora de llegada, así que no colaría.
    /// </summary>
    private IEnumerable<ILogEntry> RequestsSinceReset =>
        _server.LogEntries.Where(entry => entry.RequestMessage!.DateTime >= _countingFrom);

    /// <summary>Cuántas veces se pidió el directorio completo desde el último <c>Serve*</c>.</summary>
    public int ListRequestCount => RequestsSinceReset.Count(entry =>
        entry.RequestMessage?.Path == OfficesPath
        && string.Equals(entry.RequestMessage?.Method, "GET", StringComparison.OrdinalIgnoreCase));

    /// <summary>Cuántas veces se pidió el volcado de empresas desde el último <c>Serve*</c>.</summary>
    public int CompanyDirectoryRequestCount => RequestsSinceReset.Count(entry =>
        entry.RequestMessage?.Path == CompanyDirectoryPath
        && string.Equals(entry.RequestMessage?.Method, "GET", StringComparison.OrdinalIgnoreCase));

    /// <summary>Cuántas veces se pidieron los contactos de esta empresa desde el último <c>Serve*</c>.</summary>
    public int CompanyContactsRequestCount(Guid companyId) => RequestsSinceReset.Count(entry =>
        entry.RequestMessage?.Path == $"{CompaniesPath}/{companyId}/contacts"
        && string.Equals(entry.RequestMessage?.Method, "GET", StringComparison.OrdinalIgnoreCase));

    /// <summary>Cuántas veces se pidió esta empresa por id desde el último <c>Serve*</c>.</summary>
    public int CompanyRequestCount(Guid companyId) => RequestsSinceReset.Count(entry =>
        entry.RequestMessage?.Path == $"{CompaniesPath}/{companyId}"
        && string.Equals(entry.RequestMessage?.Method, "GET", StringComparison.OrdinalIgnoreCase));

    /// <summary>Cuántas veces se pidieron contactos por id (<c>GET v1/contacts?id=</c>) desde el último corte.</summary>
    public int ContactsByIdRequestCount => RequestsSinceReset.Count(entry =>
        entry.RequestMessage?.Path == ContactsPath
        && string.Equals(entry.RequestMessage?.Method, "GET", StringComparison.OrdinalIgnoreCase));

    /// <summary>Cuántas consultas de contactabilidad llegaron desde el último corte, y de cuántos ids cada una.</summary>
    public IReadOnlyList<int> MailabilityBatchSizes => RequestsSinceReset
        .Where(entry => entry.RequestMessage?.Path == MailabilityPath)
        .OrderBy(entry => entry.RequestMessage!.DateTime)
        .Select(entry => ReadIds(entry.RequestMessage!.Body).Count)
        .ToList();

    /// <summary>
    /// El CRM dice esto del contacto desde ahora: el motivo del correo (<c>opted_out</c>,
    /// <c>do_not_invite</c>…) y, aparte, el de la invitación (<c>suspended</c>, <c>do_not_invite</c>
    /// o <c>null</c> si se le puede invitar).
    /// </summary>
    public void ServeVerdict(Guid contactId, string? suppressionReason, string? inviteSuppressionReason)
    {
        _unknownContacts.TryRemove(contactId, out _);
        _verdicts[contactId] = new StubMailability(suppressionReason, inviteSuppressionReason, OmitsCanBeInvited: false);
    }

    /// <summary>
    /// El CRM contesta por el contacto sin <c>canBeInvited</c> ni su motivo, como uno anterior a
    /// PGTI-190.
    /// </summary>
    public void ServeVerdictWithoutInvitation(Guid contactId)
    {
        _unknownContacts.TryRemove(contactId, out _);
        _verdicts[contactId] = new StubMailability(null, null, OmitsCanBeInvited: true);
    }

    /// <summary>El CRM deja de tener fila para el contacto, como con uno borrado.</summary>
    public void ForgetContact(Guid contactId) => _unknownContacts[contactId] = true;

    /// <summary>
    /// La contactabilidad contesta 500, sin tocar ni OPT ni las empresas. Ojo: el circuito del CRM es
    /// uno solo, así que fallar demasiado lo abre también para el resto de las rutas.
    /// </summary>
    public void MailabilityGoesDown() => _mailabilityDown = true;

    /// <summary>
    /// La lectura de contactos por id contesta 500, sin tocar las empresas ni la contactabilidad. El
    /// mismo aviso que arriba: el circuito del CRM es uno solo.
    /// </summary>
    public void ContactsGoDown() => _contactsDown = true;

    /// <summary>La oficina y el contacto por omisión, ambos vigentes y en el directorio.</summary>
    public void ServeDefaultDirectory() =>
        Serve(new StubOptOffice(OfficeId, OfficeName, IsActive: true,
            [new StubOptStaff(ContactId, ContactName, IsDirector: true, IsActive: true)]));

    /// <summary>
    /// Publica este directorio y descarta lo anterior, contador de peticiones incluido. Cada
    /// oficina se resuelve por id; solo las marcadas aparecen en el listado, que es lo que hace el
    /// CRM real —el listado es el desplegable y solo trae lo elegible—.
    /// </summary>
    public void Serve(params StubOptOffice[] offices)
    {
        ResetServer();
        ApplyCompanies();
        ApplyMailability();
        ApplyContactsById();

        _server
            .Given(Request.Create().WithPath(OfficesPath).UsingGet())
            .AtPriority(1)
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(offices.Where(o => o.ListedInDirectory).Select(ToListItem)));

        foreach (var office in offices)
        {
            _server
                .Given(Request.Create().WithPath($"{OfficesPath}/{office.Id}").UsingGet())
                .AtPriority(1)
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithBodyAsJson(ToDetail(office)));
        }

        // Un id que el CRM no conoce es 404, no una avería.
        _server
            .Given(Request.Create().WithPath($"{OfficesPath}/*").UsingGet())
            .AtPriority(9)
            .RespondWith(Response.Create().WithStatusCode(404));
    }

    /// <summary>
    /// El CRM deja de contestar el directorio de OPT. Se responde 500 y no se apaga el servidor para
    /// conservar el puerto: la URL base ya está en la configuración del host y no se puede cambiar
    /// en caliente.
    /// </summary>
    public void GoDown()
    {
        ResetServer();
        ApplyCompanies();
        ApplyMailability();
        ApplyContactsById();

        _server
            .Given(Request.Create().WithPath($"{OfficesPath}*").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500));
    }

    /// <summary>
    /// Cualquier id de empresa resuelve, viva y activa. Es el modo por omisión, para la mayoría de
    /// los tests: crean proveedores con ids inventados y lo que ejercitan no es la validación, así
    /// que declarar cada empresa de antemano sería ruido. Un test que sí va sobre la validación pasa
    /// a <see cref="ServeCompanies"/>, donde lo que no está declarado no existe.
    /// </summary>
    public void ServeAnyCompany()
    {
        _companies = null;
        ApplyCompanies();
    }

    /// <summary>
    /// Publica estas empresas y solo estas: cualquier otro id contesta 404, que es una respuesta del
    /// CRM y no una avería. Cada una se resuelve por id incluso dormida o dada de baja, porque la
    /// lectura por id es la de mostrar y validar, no el desplegable de quien elige.
    /// </summary>
    public void ServeCompanies(params StubCrmCompany[] companies)
    {
        _companies = companies;
        ApplyCompanies();
    }

    /// <summary>
    /// Publica los contactos de una empresa, reemplazando los que tuviera. Una empresa sin contactos
    /// publicados responde la página vacía, que es lo que hace el CRM real: no tener a quién llamar
    /// es una respuesta, no una avería.
    /// </summary>
    public void ServeContacts(Guid companyId, params StubCrmContact[] contacts)
    {
        _contacts[companyId] = contacts;
        ApplyCompanies();
    }

    /// <summary>
    /// Publica las cuentas bancarias de una empresa, reemplazando las que tuviera. Se leen por id y
    /// solo dentro de su empresa: pedir una con el id de otra empresa contesta 404.
    /// </summary>
    public void ServeBankAccounts(Guid companyId, params StubBankAccount[] accounts)
    {
        _bankAccounts[companyId] = accounts;
        ApplyCompanies();
    }

    /// <summary>
    /// El CRM contesta los contactos de esta empresa con un cuerpo sin <c>items</c>. Es un 200 y no
    /// una avería, así que no entra por la rama de la caída: reproduce el cuerpo degenerado que
    /// haría reventar a quien confíe en que la lista siempre viene.
    /// </summary>
    public void ServeContactsWithNoItems(Guid companyId)
    {
        _server
            .Given(Request.Create().WithPath($"{CompaniesPath}/{companyId}/contacts").UsingGet())
            .WithGuid(TrackCompanyMapping())
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new { }));
    }

    /// <summary>
    /// El CRM deja de contestar las empresas —el volcado y la lectura por id—, sin tocar el
    /// directorio de OPT. Los dos directorios salen del mismo CRM, pero separarlos acá es lo que
    /// permite ver que una escritura de proveedor se rechaza por su propia consulta y no de rebote
    /// por otra.
    /// </summary>
    public void CompaniesGoDown()
    {
        _server
            .Given(Request.Create().WithPath($"{CompaniesPath}/*").UsingGet())
            .WithGuid(CompaniesOutageMappingId)
            .AtPriority(0)
            .RespondWith(Response.Create().WithStatusCode(500));
    }

    /// <summary>
    /// Olvida las peticiones registradas sin tocar lo que se sirve, y mueve el corte: lo que entró
    /// antes de esta línea no es de este test aunque el CRM lo conteste dentro de él.
    /// </summary>
    public void ResetRequestLog()
    {
        _server.ResetLogEntries();
        _countingFrom = DateTime.UtcNow;
    }

    /// <summary>
    /// Vuelve a empezar: se van las reglas y el registro, y el corte se mueve con ellos. Es la
    /// única puerta por la que se reinicia el servidor, para que ningún <c>Serve*</c> nuevo olvide
    /// mover el corte y herede las peticiones del test anterior.
    /// </summary>
    private void ResetServer()
    {
        _server.Reset();
        _countingFrom = DateTime.UtcNow;
    }

    /// <summary>
    /// La ruta de contactabilidad. Contesta por cada id pedido según lo declarado, en el momento de
    /// la petición, y omite los que el CRM no conoce, igual que el real.
    /// </summary>
    private void ApplyMailability() =>
        _server
            .Given(Request.Create().WithPath(MailabilityPath).UsingPost())
            .WithGuid(MailabilityMappingId)
            .RespondWith(Response.Create().WithCallback(request =>
            {
                if (_mailabilityDown)
                    return new ResponseMessage { StatusCode = 500 };

                var verdicts = ReadIds(request.Body)
                    .Where(id => !_unknownContacts.ContainsKey(id))
                    .Select(id =>
                    {
                        var verdict = _verdicts.GetValueOrDefault(id) ?? StubMailability.Clear;
                        var row = new Dictionary<string, object?>
                        {
                            ["id"] = id,
                            ["canBeEmailed"] = verdict.SuppressionReason is null,
                            ["suppressionReason"] = verdict.SuppressionReason,
                        };
                        if (!verdict.OmitsCanBeInvited)
                        {
                            row["canBeInvited"] = verdict.InviteSuppressionReason is null;
                            row["inviteSuppressionReason"] = verdict.InviteSuppressionReason;
                        }

                        return row;
                    });

                return new ResponseMessage
                {
                    StatusCode = 200,
                    Headers = new Dictionary<string, WireMockList<string>> { ["Content-Type"] = new("application/json") },
                    BodyData = new BodyData
                    {
                        BodyAsString = JsonSerializer.Serialize(verdicts),
                        DetectedBodyType = BodyType.String,
                        Encoding = Encoding.UTF8,
                    },
                };
            }));

    /// <summary>
    /// La lectura de contactos por id, paginada como la del CRM real. Contesta los contactos
    /// publicados en cualquier empresa, en el momento de la petición, y omite los que no conoce:
    /// un id desconocido no es un error.
    /// </summary>
    private void ApplyContactsById() =>
        _server
            .Given(Request.Create().WithPath(ContactsPath).UsingGet())
            .WithGuid(ContactsByIdMappingId)
            .RespondWith(Response.Create().WithCallback(request =>
            {
                if (_contactsDown)
                    return new ResponseMessage { StatusCode = 500 };

                var asked = request.Query is not null && request.Query.TryGetValue("id", out var ids)
                    ? ids.Select(Guid.Parse).ToHashSet()
                    : [];
                var known = _contacts.Values
                    .SelectMany(contacts => contacts)
                    .Where(contact => asked.Contains(contact.Id))
                    .DistinctBy(contact => contact.Id)
                    .Select(contact => new { id = contact.Id, name = contact.Name })
                    .ToList();

                return new ResponseMessage
                {
                    StatusCode = 200,
                    Headers = new Dictionary<string, WireMockList<string>> { ["Content-Type"] = new("application/json") },
                    BodyData = new BodyData
                    {
                        BodyAsString = JsonSerializer.Serialize(new
                        {
                            items = known,
                            totalCount = known.Count,
                            page = 1,
                            pageSize = 100,
                        }),
                        DetectedBodyType = BodyType.String,
                        Encoding = Encoding.UTF8,
                    },
                };
            }));

    private static IReadOnlyList<Guid> ReadIds(string? body) =>
        body is null
            ? []
            : JsonDocument.Parse(body).RootElement.GetProperty("ids").EnumerateArray().Select(e => e.GetGuid()).ToList();

    /// <summary>Programa las rutas de empresa que correspondan al modo vigente.</summary>
    private void ApplyCompanies()
    {
        // Volver a publicar empresas es también levantar el CRM: sin esto, la regla de la caída
        // seguiría ganando por prioridad y el test que comprueba que nada se escribió no podría
        // distinguir "no se escribió" de "el CRM sigue caído".
        _server.DeleteMapping(CompaniesOutageMappingId);

        foreach (var mappingId in _companyMappingIds)
            _server.DeleteMapping(mappingId);

        _companyMappingIds.Clear();

        // El volcado va por delante de la ruta por id: "directory" no es un guid, y sin esta
        // prioridad el comodín de abajo lo contestaría como si fuera una empresa.
        _server
            .Given(Request.Create().WithPath(CompanyDirectoryPath).UsingGet())
            .WithGuid(TrackCompanyMapping())
            .AtPriority(1)
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson((_companies ?? []).Where(c => c.ListedInDirectory).Select(ToDirectoryEntry)));

        // Los contactos van por delante de la ruta por id: "{id}/contacts" también encaja en el
        // comodín de abajo, y sin esta prioridad la ficha recibiría una empresa donde espera una
        // página de contactos.
        foreach (var (companyId, contacts) in _contacts)
        {
            _server
                .Given(Request.Create().WithPath($"{CompaniesPath}/{companyId}/contacts").UsingGet())
                .WithGuid(TrackCompanyMapping())
                .AtPriority(1)
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithBodyAsJson(ToContactPage(contacts)));
        }

        // Las cuentas van por delante de la ruta por id por lo mismo que los contactos. Una cuenta
        // sin publicar contesta 404, que es lo que hace el CRM con la de otra empresa.
        foreach (var (companyId, accounts) in _bankAccounts)
        {
            foreach (var account in accounts)
            {
                _server
                    .Given(Request.Create().WithPath($"{CompaniesPath}/{companyId}/bank-accounts/{account.Id}").UsingGet())
                    .WithGuid(TrackCompanyMapping())
                    .AtPriority(1)
                    .RespondWith(Response.Create()
                        .WithStatusCode(200)
                        .WithBodyAsJson(ToBankAccount(companyId, account)));
            }
        }

        _server
            .Given(Request.Create().WithPath($"{CompaniesPath}/*/bank-accounts/*").UsingGet())
            .WithGuid(TrackCompanyMapping())
            .AtPriority(2)
            .RespondWith(Response.Create().WithStatusCode(404));

        // Una empresa sin contactos publicados contesta la página vacía. Se declara explícitamente
        // porque el comodín de empresa de abajo contestaría una empresa, y la ficha no podría
        // distinguir "no hay a quién llamar" de "el CRM devolvió cualquier cosa".
        _server
            .Given(Request.Create().WithPath($"{CompaniesPath}/*/contacts").UsingGet())
            .WithGuid(TrackCompanyMapping())
            .AtPriority(2)
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(ToContactPage([])));

        if (_companies is null)
        {
            // El id del cuerpo sale de la ruta pedida, para que la respuesta no contradiga la
            // pregunta aunque hoy nadie las compare.
            _server
                .Given(Request.Create().WithPath($"{CompaniesPath}/*").UsingGet())
                .WithGuid(TrackCompanyMapping())
                .AtPriority(9)
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithBodyAsJson(ToCompany(new StubCrmCompany(
                        Guid.Empty, CompanyName, IsActive: true, IsDeleted: false)))
                    .WithTransformer());

            return;
        }

        foreach (var company in _companies)
        {
            _server
                .Given(Request.Create().WithPath($"{CompaniesPath}/{company.Id}").UsingGet())
                .WithGuid(TrackCompanyMapping())
                .AtPriority(2)
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithBodyAsJson(ToCompany(company)));
        }

        _server
            .Given(Request.Create().WithPath($"{CompaniesPath}/*").UsingGet())
            .WithGuid(TrackCompanyMapping())
            .AtPriority(9)
            .RespondWith(Response.Create().WithStatusCode(404));
    }

    /// <summary>
    /// Una cuenta como la lee el CRM: el número siempre enmascarado, y la desactivada con su fecha,
    /// porque por id se sigue sirviendo.
    /// </summary>
    private static object ToBankAccount(Guid companyId, StubBankAccount account) => new
    {
        id = account.Id,
        companyId,
        holder = CompanyName,
        bankName = "Banco Popular Dominicano",
        bankCountry = "DO",
        currency = "DOP",
        accountNumberMasked = "****1234",
        swiftBic = "BPDODOSXXXX",
        isPrincipal = false,
        deactivatedAt = account.IsActive ? (DateTimeOffset?)null : DateTimeOffset.UtcNow.AddDays(-1)
    };

    /// <summary>Reserva el identificador de una regla de empresa para poder retirarla luego.</summary>
    private Guid TrackCompanyMapping()
    {
        var mappingId = Guid.NewGuid();
        _companyMappingIds.Add(mappingId);
        return mappingId;
    }

    /// <summary>El listado del CRM: solo oficinas vivas, y de cada una solo su personal vivo.</summary>
    private static object ToListItem(StubOptOffice office) => new
    {
        id = office.Id,
        code = office.Code,
        name = office.Name,
        country = "España",
        city = "Madrid",
        isActive = office.IsActive,
        staff = office.Staff.Where(s => s.IsActive).Select(ToStaff)
    };

    /// <summary>La resolución por id: la oficina aunque esté cerrada, con todo su personal.</summary>
    private static object ToDetail(StubOptOffice office) => new
    {
        id = office.Id,
        code = office.Code,
        name = office.Name,
        country = "España",
        city = "Madrid",
        streetAddress1 = (string?)null,
        streetAddress2 = (string?)null,
        phone = (string?)null,
        email = (string?)null,
        isActive = office.IsActive,
        staff = office.Staff.Select(ToStaff)
    };

    /// <summary>
    /// El volcado del directorio: cinco campos y ninguno más, con <c>isActive</c> fundido en un solo
    /// bit —si la empresa todavía se puede elegir—, que es lo que emite el CRM real.
    /// </summary>
    private static object ToDirectoryEntry(StubCrmCompany company) => new
    {
        id = company.Id,
        name = company.Name,
        country = company.Country,
        city = company.City,
        isActive = company.IsActive && !company.IsDeleted
    };

    /// <summary>
    /// La lectura por id de una empresa. <c>isActive</c> e <c>isDeleted</c> viajan separados porque
    /// son dos ejes distintos, y esta lectura devuelve cualquier combinación de los dos.
    /// </summary>
    private static object ToCompany(StubCrmCompany company) => new
    {
        // Guid.Empty es el centinela del modo permisivo: ahí el id sale de la ruta pedida, que es lo
        // que resuelve la plantilla.
        id = company.Id == Guid.Empty ? "{{request.PathSegments.[2]}}" : company.Id.ToString(),
        name = company.Name,
        commercialName = (string?)null,
        description = (string?)null,
        domain = (string?)null,
        domainRaw = (string?)null,
        country = company.Country,
        city = company.City,
        streetAddress1 = (string?)null,
        streetAddress2 = (string?)null,
        phone = (string?)null,
        email = (string?)null,
        isActive = company.IsActive,
        isDeleted = company.IsDeleted,
        optOfficeId = OfficeId,
        categories = Array.Empty<object>()
    };

    /// <summary>
    /// La página de contactos de una empresa, con la forma paginada que emite el CRM real.
    /// </summary>
    private static object ToContactPage(IReadOnlyList<StubCrmContact> contacts) => new
    {
        items = contacts.Select(ToContact),
        totalCount = contacts.Count,
        page = 1,
        pageSize = 100
    };

    /// <summary>
    /// Un contacto de empresa. <c>canBeEmailed</c> viaja porque es el eje de consentimiento del CRM,
    /// y la ficha del proveedor tiene que devolver también al que lo retiró: llamar a un proveedor
    /// por un trabajo ya contratado no es mercadeo.
    /// </summary>
    private static object ToContact(StubCrmContact contact) => new
    {
        companyContactId = Guid.NewGuid(),
        contactId = contact.Id,
        name = contact.Name,
        email = contact.Email,
        phone = contact.Phone,
        jobTitle = contact.JobTitle,
        department = "Ventas",
        startDate = "2020-01-01",
        endDate = (string?)null,
        canBeEmailed = contact.CanBeEmailed,
        suppressionReason = contact.CanBeEmailed ? null : "opt_out"
    };

    private static object ToStaff(StubOptStaff staff) => new
    {
        id = staff.Id,
        fullName = staff.FullName,
        role = "Director",
        isDirector = staff.IsDirector,
        isActive = staff.IsActive,
        email = (string?)null,
        phone = (string?)null
    };

    public void Dispose() => _server.Dispose();
}

/// <summary>
/// Una oficina del CRM simulado. <c>ListedInDirectory</c> en <c>false</c> reproduce el caso que
/// obliga a resolver por id: la oficina existe y se puede mostrar, pero no sale en el listado.
/// </summary>
/// <param name="Code">
/// El código de la oficina, el mismo que el realm guarda en <c>opt_code</c>. Dejó de ser
/// decorativo cuando el aviso del sometimiento empezó a usarlo como puente entre el id que guarda
/// un evento y la gente que el realm reconoce por su oficina.
/// </param>
public sealed record StubOptOffice(
    Guid Id,
    string Name,
    bool IsActive,
    IReadOnlyList<StubOptStaff> Staff,
    bool ListedInDirectory = true,
    string Code = CrmDirectoryStub.OfficeCode);

/// <summary>Una persona del CRM simulado.</summary>
public sealed record StubOptStaff(Guid Id, string FullName, bool IsDirector, bool IsActive);

/// <summary>
/// Una empresa del CRM simulado. <c>IsActive</c> e <c>IsDeleted</c> son dos ejes distintos —la
/// relación comercial y la baja del registro—: una empresa dormida y una dada de baja resuelven las
/// dos, porque un proveedor registrado hace tres años tiene que seguir editándose.
/// <para>
/// <c>ListedInDirectory</c> en <c>false</c> reproduce el caso que obliga a resolver por id: la
/// empresa existe y se puede mostrar, pero el volcado no la trae. Es la cola larga del directorio y
/// no un síntoma de nada.
/// </para>
/// </summary>
public sealed record StubCrmCompany(
    Guid Id,
    string Name,
    bool IsActive,
    bool IsDeleted,
    string Country = CrmDirectoryStub.CompanyCountry,
    string? City = CrmDirectoryStub.CompanyCity,
    bool ListedInDirectory = true);

/// <summary>
/// Un contacto de empresa del CRM simulado. <c>CanBeEmailed</c> en <c>false</c> reproduce a quien
/// retiró su consentimiento de mercadeo: sigue apareciendo en la ficha del proveedor, porque su
/// teléfono es lo que permite resolver un evento en curso.
/// </summary>
public sealed record StubCrmContact(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    string? JobTitle = "Gerente de Ventas",
    bool CanBeEmailed = true);

/// <summary>Una cuenta bancaria del CRM simulado. Una inactiva es una cuenta desactivada.</summary>
public sealed record StubBankAccount(Guid Id, bool IsActive = true);

/// <summary>Lo que el CRM simulado contesta por un contacto en la ruta de contactabilidad.</summary>
internal sealed record StubMailability(string? SuppressionReason, string? InviteSuppressionReason, bool OmitsCanBeInvited)
{
    public static readonly StubMailability Clear = new(null, null, OmitsCanBeInvited: false);
}
