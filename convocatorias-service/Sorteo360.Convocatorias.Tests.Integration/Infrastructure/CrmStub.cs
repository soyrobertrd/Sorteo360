using System.Collections.Concurrent;
using System.Text.Json;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Types;
using WireMock.Util;

namespace Sorteo360.Convocatorias.Tests.Integration.Infrastructure;

/// <summary>
/// El CRM visto desde convocatorias-service: el listado de contactos, la búsqueda exacta
/// por correo, el alta con su 409, la consulta de mailability y la baja, y lo que escribe la carga
/// de proveedores: el alta de una empresa, el catálogo de fuentes de origen y la afiliación.
/// </summary>
public sealed class CrmStub : NeighbourStub
{
    private const string ContactsPath = "/v1/contacts";
    /// <summary>
    /// La recomprobación de contactabilidad. Es <c>internal</c> y no privada porque los tests del
    /// lanzamiento la nombran para reconocerla en el registro de peticiones, y escribir la ruta a
    /// mano en cada clase es cómo una de ellas se queda vieja.
    /// </summary>
    internal const string MailabilityPath = "/v1/contacts/mailability";
    private const string OptOutPattern = $"{ContactsPath}/*/opt-out";
    private const string OptOutSuffix = "/opt-out";
    private const string CompaniesPath = "/v1/companies";
    private const string AffiliationPattern = $"{CompaniesPath}/*/contacts";
    private const string BankAccountPattern = $"{CompaniesPath}/*/bank-accounts";
    private const string OriginSourcesPath = "/v1/origin-sources";
    private const string CompanyMatchPath = "/v1/companies/match";
    private const string CompanyDirectoryPath = "/v1/companies/directory";
    private const string ExistingEmailsPath = "/v1/contacts/existing-emails";

    /// <summary>El nombre de la oficina dueña con el que contesta la búsqueda de empresa.</summary>
    public const string MatchedOfficeName = "Oficina previa";

    /// <summary>
    /// El código genérico del chasis para un choque con un índice único. Es lo que contesta el CRM
    /// de verdad al afiliar dos veces el mismo contacto a la misma empresa.
    /// </summary>
    public const string DbConflictCode = "DB_CONFLICT";

    /// <summary>La fuente de origen que crea el paso de despliegue de la carga de proveedores.</summary>
    public static readonly Guid SupplierIntakeOriginSourceId = Guid.Parse("6f1a2b3c-4d5e-4f60-8a71-b2c3d4e5f607");

    public const string SupplierIntakeOriginSourceName = "Carga de proveedores OPT";

    /// <summary>
    /// Los códigos con los que el CRM rechaza el alta de una empresa repetida. Son los de
    /// <c>DuplicateCompanyNameError</c> y <c>DuplicateCompanyDomainError</c> del CRM de verdad.
    /// </summary>
    public const string DuplicateCompanyNameCode = "company.duplicate_name";

    public const string DuplicateCompanyDomainCode = "company.duplicate_domain";

    /// <summary>Un contacto vigente y contactable, el que usan los tests que no van sobre el CRM.</summary>
    public static readonly Guid ContactId = Guid.Parse("2c7e3f4a-6b1d-4e8f-9a0b-1c2d3e4f5061");

    public const string ContactEmail = "ana@acme.com";
    public const string ContactFirstName = "Ana";
    public const string ContactLastName = "Pérez";

    /// <summary>Un contacto que se dio de baja: existe, se lista, y no se le puede escribir.</summary>
    public static readonly Guid SuppressedContactId = Guid.Parse("3d8f4a5b-7c2e-4f90-8b1c-2d3e4f506172");

    public const string SuppressedContactEmail = "baja@acme.com";
    public const string OptOutSuppressionReason = "opted_out";

    /// <summary>
    /// El código con el que el CRM rechaza un alta sobre una dirección ocupada, y el nombre de
    /// la extensión que trae la ficha viva. Son los del CRM de verdad
    /// (<c>DuplicateContactEmailError</c>), no los que este doble se invente: un doble que
    /// contesta otra forma esconde exactamente el fallo que tiene que cazar.
    /// </summary>
    public const string DuplicateEmailCode = "DUPLICATE_CONTACT_EMAIL";

    /// <summary>El CRM lee y escribe camelCase, igual que el resto de la plataforma.</summary>
    private static readonly JsonSerializerOptions CamelCase =
        new(JsonSerializerDefaults.Web);

    private int _successfulContactCreates;

    /// <summary>
    /// Cuántas altas seguidas contesta bien antes de que el CRM deje de estar. Se cuenta sobre
    /// las que <b>salieron</b>, no sobre los intentos, para que los reintentos del chasis no
    /// muevan el punto de corte.
    /// </summary>
    private int _failContactCreateAfter = int.MaxValue;

    /// <summary>
    /// Las empresas que este CRM "tiene": las que se crearon por la ruta de alta y las que un test
    /// sembró. Sobreviven a <c>ServeDefaults</c> a propósito, porque un reenvío tiene que encontrar
    /// lo que dejó escrito el envío que falló, como pasa con el CRM de verdad.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, StubCompany> _companies = new();

    /// <summary>Las empresas que ya tienen alguna cuenta bancaria viva.</summary>
    private readonly ConcurrentDictionary<Guid, byte> _companiesWithAccount = new();

    /// <summary>Las afiliaciones vivas, empresa y contacto.</summary>
    private readonly ConcurrentDictionary<(Guid CompanyId, Guid ContactId), byte> _affiliations = new();

    /// <summary>Los correos que ya tienen contacto, plegados, con el id de ese contacto.</summary>
    private readonly ConcurrentDictionary<string, Guid> _contactEmails = new();

    public CrmStub() => ConfigureDefaults();

    /// <summary>
    /// Siembra una empresa que el CRM ya tenía antes del envío, de otra oficina, y devuelve su id.
    /// </summary>
    public Guid RememberCompany(string name, string country, string? website = null)
    {
        var id = Guid.NewGuid();
        _companies[id] = new StubCompany(name, country, website, Guid.NewGuid());
        return id;
    }

    /// <summary>Siembra un contacto que ya ocupa ese correo, para la consulta de correos existentes.</summary>
    public void RememberContactEmail(string email) => _contactEmails[Folded(email)!] = Guid.NewGuid();

    /// <summary>Siembra una cuenta bancaria en una empresa.</summary>
    public void RememberBankAccount(Guid companyId) => _companiesWithAccount[companyId] = 0;

    /// <summary>Siembra una afiliación viva de un contacto con una empresa.</summary>
    public void RememberAffiliation(Guid companyId, Guid contactId) => _affiliations[(companyId, contactId)] = 0;

    /// <summary>El id de la empresa con ese nombre exacto, o <c>null</c> si el CRM no la tiene.</summary>
    public Guid? CompanyIdNamed(string name) =>
        _companies.Where(pair => pair.Value.Name == name).Select(pair => (Guid?)pair.Key).SingleOrDefault();

    /// <summary>
    /// La URL completa del último listado de contactos que recibió, o <c>null</c> si no recibió
    /// ninguno. Es lo que permite comprobar que los filtros de una selección llegan repetidos y
    /// escapados, en vez de dar por hecho que el cliente los compuso bien.
    /// </summary>
    public string? LastContactListUrl => RequestsSinceReset
        .Select(entry => entry.RequestMessage)
        .Where(request => request is not null
            && request.Path == ContactsPath
            && string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
        .Select(request => request!.Url)
        .LastOrDefault();

    /// <summary>
    /// Cuántos contactos llegó a crear. Es lo que prueba que un reintento de una confirmación no
    /// vuelve a crear lo que ya creó: sale el total de filas nuevas y ni uno más.
    /// </summary>
    public int SuccessfulContactCreateCount => Volatile.Read(ref _successfulContactCreates);

    /// <summary>
    /// Lo que recibió desde el último corte, en orden de llegada, como <c>MÉTODO /ruta</c>. Es
    /// con lo que se comprueba que el lanzamiento vacía las bajas <b>antes</b> de preguntar por
    /// la contactabilidad: el orden es la funcionalidad, y ninguna cuenta lo demuestra.
    /// </summary>
    public IReadOnlyList<string> CallsSinceReset =>
    [
        .. OrderedRequests().Select(request => $"{request.Method.ToUpperInvariant()} {request.Path}"),
    ];

    /// <summary>A quién se le empujó una baja, en orden de llegada.</summary>
    public IReadOnlyList<Guid> OptedOutContactIds =>
    [
        .. OrderedRequests()
            .Where(request => string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase)
                && request.Path.EndsWith(OptOutSuffix, StringComparison.Ordinal))
            .Select(request => CompanyIdFromPath(request)),
    ];

    /// <summary>Cuántas veces se le preguntó por la contactabilidad de una lista.</summary>
    public int MailabilityCallCount => CallsSinceReset.Count(call => call.EndsWith(MailabilityPath, StringComparison.Ordinal));

    /// <summary>
    /// Los cuerpos de las altas que recibió desde el último corte, en orden de llegada. Es con lo
    /// que se comprueba <b>qué</b> se le mandó al CRM y no solo cuántas veces: el consentimiento
    /// y su origen son el dato de la spec <c>06</c> §4.2, y una cuenta no dice nada de ellos.
    /// </summary>
    public IReadOnlyList<CreatedContactBody> ContactCreateBodies =>
    [
        .. OrderedRequests()
            .Where(request => string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase)
                && request.Path == ContactsPath)
            .Select(request => JsonSerializer.Deserialize<CreatedContactBody>(
                request.Body ?? "{}",
                CamelCase)!),
    ];

    /// <inheritdoc />
    protected override void ConfigureDefaults()
    {
        ServeContacts(
            new StubContact(ContactId, ContactFirstName, ContactLastName, ContactEmail, CanBeEmailed: true, SuppressionReason: null),
            new StubContact(SuppressedContactId, "Luis", "Gómez", SuppressedContactEmail, CanBeEmailed: false, OptOutSuppressionReason));
    }

    /// <summary>
    /// Publica este directorio de contactos y descarta lo anterior. Sirve el listado, el
    /// filtro exacto por correo, la consulta de mailability y la baja.
    /// </summary>
    public void ServeContacts(params StubContact[] contacts)
    {
        ResetServer();
        Volatile.Write(ref _successfulContactCreates, 0);
        Volatile.Write(ref _failContactCreateAfter, int.MaxValue);

        Server
            .Given(Request.Create().WithPath(MailabilityPath).UsingPost())
            .AtPriority(1)
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(contacts.Select(c => new
                {
                    id = c.Id,
                    canBeEmailed = c.CanBeEmailed,
                    suppressionReason = c.SuppressionReason,
                })));

        // El listado y el filtro exacto comparten ruta, así que quien contesta mira la consulta:
        // con `email=` devuelve solo lo que casa exactamente, y sin él devuelve todo. Servir el
        // listado entero ante un `email=` desconocido ataría una fila a la ficha de un
        // desconocido, que es el fallo que el filtro exacto existe para evitar.
        Server
            .Given(Request.Create().WithPath(ContactsPath).UsingGet())
            .AtPriority(5)
            .RespondWith(Response.Create().WithCallback(request =>
            {
                var email = Query(request, "email");
                var ids = QueryIds(request);

                var matching = email is null
                    ? contacts
                    : [.. contacts.Where(c => string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase))];

                // Con `id=` el CRM devuelve solo los pedidos que tiene, y calla los demás.
                if (ids.Count > 0)
                    matching = [.. matching.Where(c => ids.Contains(c.Id))];

                return Json(200, Page(matching));
            }));

        ServeContactCreate();
        ServeSupplierIntakeRoutes();

        Server
            .Given(Request.Create().WithPath(OptOutPattern).UsingPost())
            .AtPriority(1)
            .RespondWith(Response.Create().WithStatusCode(200));
    }

    /// <summary>
    /// El CRM contesta esto y solo esto a la recomprobación, por encima de lo que sirve
    /// <see cref="ServeContacts"/>. Es como se llega a "estaba contactable al seleccionar y ya no
    /// al lanzar", que es la carrera entera que la recomprobación existe para cazar.
    /// <para>
    /// <b>Un contacto que no se le pase queda fuera de la respuesta</b>, que es como el CRM
    /// informa de una ficha que ya no devuelve: no puede opinar sobre lo que no tiene.
    /// </para>
    /// </summary>
    public void ServeMailability(params StubContact[] verdicts) =>
        Server
            .Given(Request.Create().WithPath(MailabilityPath).UsingPost())
            // Por delante de la que ya sirve `ServeContacts`: se añade encima en vez de
            // reprogramar el servidor, así que quien decide entre las dos es la precedencia y el
            // registro de peticiones de este test se conserva.
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(verdicts.Select(contact => new
                {
                    id = contact.Id,
                    canBeEmailed = contact.CanBeEmailed,
                    suppressionReason = contact.SuppressionReason,
                })));

    /// <summary>
    /// El CRM deja de contestar <b>solo</b> a la recomprobación. Lo demás sigue en pie, que es lo
    /// que permite llegar hasta el segundo paso del lanzamiento y fallar ahí: con el vecino
    /// entero caído el vaciado se para antes y esa rama nunca se ejercita.
    /// </summary>
    public void FailMailability() =>
        Server
            .Given(Request.Create().WithPath(MailabilityPath).UsingPost())
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(503)
                .WithBodyAsJson(new { detail = "El CRM no está disponible." }));

    /// <summary>
    /// El CRM ya no tiene la ficha a la que se le empuja la baja: 404, que es lo que contesta su
    /// ruta real ante un contacto borrado en blando. No es una avería, y el lanzamiento sigue.
    /// </summary>
    public void ServeOptOutContactGone() =>
        Server
            .Given(Request.Create().WithPath(OptOutPattern).UsingPost())
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(404));

    /// <summary>El CRM deja de contestar solo al empujón de una baja.</summary>
    public void FailOptOut() =>
        Server
            .Given(Request.Create().WithPath(OptOutPattern).UsingPost())
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(503)
                .WithBodyAsJson(new { detail = "El CRM no está disponible." }));

    /// <summary>
    /// Sirve una lista de contactos por id con la forma de <c>GET /v1/contact-lists/{id}</c> del
    /// CRM, como la ve una cuenta de servicio: sin acotar por OPT. Una lista que no se sirve
    /// contesta 404, que es lo que hace WireMock con una ruta sin programar.
    /// </summary>
    public void ServeContactList(Guid contactListId, Guid optOfficeId, params Guid[] contactIds) =>
        Server
            .Given(Request.Create().WithPath($"/v1/contact-lists/{contactListId}").UsingGet())
            .AtPriority(1)
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new
                {
                    id = contactListId,
                    optOfficeId,
                    name = "Agencias de Madrid",
                    memberCount = contactIds.Length,
                    contactIds,
                }));

    /// <summary>Sirve una oficina del directorio de OPT con la forma de <c>GET /v1/opts/{id}</c>, con su personal.</summary>
    /// <remarks>El nombre de la oficina es <c>OPT</c> seguido del código.</remarks>
    public void ServeOptOffice(Guid optOfficeId, string code, bool isActive = true, params StubOptStaff[] staff) =>
        ServeOptOfficeAt(priority: 1, optOfficeId, code, isActive, staff);

    /// <summary>La misma oficina, ahora cerrada, con prioridad sobre lo que se sirviera antes.</summary>
    /// <remarks>Es para el test que necesita que la oficina se cierre después de crear su enlace.</remarks>
    public void ServeOptOfficeClosed(Guid optOfficeId) =>
        ServeOptOfficeAt(priority: 0, optOfficeId, "cerrada", isActive: false, []);

    /// <summary>El CRM ya no tiene la oficina, con prioridad sobre lo que se sirviera antes.</summary>
    /// <remarks>Es un <c>404</c> sin cuerpo, como el <c>Results.NotFound()</c> del CRM de verdad.</remarks>
    public void ServeOptOfficeGone(Guid optOfficeId) =>
        Server
            .Given(Request.Create().WithPath($"/v1/opts/{optOfficeId}").UsingGet())
            .AtPriority(0)
            .RespondWith(Response.Create().WithStatusCode(404));

    /// <summary>La respuesta entera de <c>GET /v1/opts/{id}</c>, con la prioridad de WireMock dada.</summary>
    private void ServeOptOfficeAt(int priority, Guid optOfficeId, string code, bool isActive, StubOptStaff[] staff) =>
        Server
            .Given(Request.Create().WithPath($"/v1/opts/{optOfficeId}").UsingGet())
            .AtPriority(priority)
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new
                {
                    id = optOfficeId,
                    code,
                    name = "OPT " + code,
                    country = "España",
                    city = (string?)null,
                    streetAddress1 = (string?)null,
                    streetAddress2 = (string?)null,
                    phone = (string?)null,
                    email = (string?)null,
                    senderEmail = (string?)null,
                    isActive,
                    staff = staff.Select(person => new
                    {
                        id = person.Id,
                        fullName = person.FullName,
                        role = "Técnico de promoción",
                        isDirector = false,
                        isActive = person.IsActive,
                        email = (string?)null,
                        phone = (string?)null,
                        fromHr = person.FromHr,
                        collaboratorId = (Guid?)null,
                    }),
                }));

    /// <summary>
    /// Sirve el directorio de OPT con la forma de <c>GET /v1/opts</c>: una oficina viva con ese
    /// código y esa dirección de envío (PGTI-168). Sin programar, la ruta contesta 404.
    /// </summary>
    public void ServeOptDirectory(string code, string? senderEmail) =>
        Server
            .Given(Request.Create().WithPath("/v1/opts").UsingGet())
            .AtPriority(1)
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new[]
                {
                    new
                    {
                        id = Guid.NewGuid(),
                        code,
                        name = "OPT " + code,
                        country = "España",
                        city = (string?)null,
                        senderEmail,
                        isActive = true,
                        staff = Array.Empty<object>(),
                    },
                }));

    /// <summary>El CRM deja de contestar solo al listado del directorio de OPT.</summary>
    public void FailOptDirectory() =>
        Server
            .Given(Request.Create().WithPath("/v1/opts").UsingGet())
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(503)
                .WithBodyAsJson(new { detail = "El CRM no está disponible." }));

    /// <summary>
    /// Los cuerpos de las altas de empresa que recibió desde el último corte, en orden de llegada.
    /// </summary>
    public IReadOnlyList<JsonElement> CompanyCreateBodies =>
    [
        .. OrderedRequests()
            .Where(request => string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase)
                && request.Path == CompaniesPath)
            .Select(request => JsonSerializer.Deserialize<JsonElement>(request.Body ?? "{}")),
    ];

    /// <summary>
    /// Las afiliaciones de contacto que recibió desde el último corte, en orden de llegada: la
    /// empresa que nombra la ruta y el cuerpo.
    /// </summary>
    public IReadOnlyList<(Guid CompanyId, JsonElement Body)> AffiliationRequests =>
    [
        .. OrderedRequests()
            .Where(request => string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase)
                && request.Path.StartsWith(CompaniesPath + "/", StringComparison.Ordinal)
                && request.Path.EndsWith("/contacts", StringComparison.Ordinal))
            .Select(request => (
                CompanyIdFromPath(request),
                JsonSerializer.Deserialize<JsonElement>(request.Body ?? "{}"))),
    ];

    /// <summary>
    /// Las altas de cuenta bancaria que recibió desde el último corte, en orden de llegada: la
    /// empresa que nombra la ruta y el cuerpo.
    /// </summary>
    public IReadOnlyList<(Guid CompanyId, JsonElement Body)> BankAccountRequests =>
    [
        .. OrderedRequests()
            .Where(request => string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase)
                && request.Path.StartsWith(CompaniesPath + "/", StringComparison.Ordinal)
                && request.Path.EndsWith("/bank-accounts", StringComparison.Ordinal))
            .Select(request => (
                CompanyIdFromPath(request),
                JsonSerializer.Deserialize<JsonElement>(request.Body ?? "{}"))),
    ];

    /// <summary>
    /// El catálogo de fuentes de origen con la forma de <c>GET /v1/origin-sources</c>, por encima
    /// del que se sirve por omisión. Es como se llega a un entorno donde nadie creó todavía la
    /// fuente de la carga de proveedores.
    /// </summary>
    public void ServeOriginSources(params string[] names) =>
        Server
            .Given(Request.Create().WithPath(OriginSourcesPath).UsingGet())
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(names.Select(name => new { id = Guid.NewGuid(), code = name.ToLowerInvariant(), name })));

    /// <summary>
    /// El CRM rechaza toda alta de empresa como repetida: 409 con el código dado y la empresa que
    /// ya existe en la extensión <c>existingCompany</c>, como el ProblemDetails del CRM de verdad.
    /// </summary>
    public void ServeCompanyCreateConflict(string code) =>
        Server
            .Given(Request.Create().WithPath(CompaniesPath).UsingPost())
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(409)
                .WithBodyAsJson(new
                {
                    status = 409,
                    detail = "Ya existe una empresa registrada con este nombre.",
                    code,
                    existingCompany = new { id = Guid.NewGuid(), name = "Acme" },
                }));

    /// <summary>
    /// El CRM le niega a este servicio el alta de cuentas bancarias, que es lo que contesta en un
    /// entorno donde la cuenta de servicio todavía no tiene <c>bank-accounts:write</c>: un 403 sin cuerpo.
    /// </summary>
    public void ServeBankAccountCreateForbidden() =>
        Server
            .Given(Request.Create().WithPath(BankAccountPattern).UsingPost())
            .AtPriority(0)
            .RespondWith(Response.Create().WithStatusCode(403));

    /// <summary>
    /// Lo que escribe la carga de proveedores: el alta de una empresa y la afiliación contestan
    /// <c>201</c> con un id nuevo en el cuerpo, como las rutas de verdad, y el catálogo de fuentes
    /// de origen trae la de la carga.
    /// </summary>
    private void ServeSupplierIntakeRoutes()
    {
        Server
            .Given(Request.Create().WithPath(CompaniesPath).UsingPost())
            .AtPriority(1)
            .RespondWith(Response.Create().WithCallback(request =>
            {
                var body = JsonSerializer.Deserialize<JsonElement>(request.Body ?? "{}");
                var id = Guid.NewGuid();
                _companies[id] = new StubCompany(
                    body.GetProperty("name").GetString()!,
                    body.GetProperty("country").GetString()!,
                    body.TryGetProperty("domain", out var domain) ? domain.GetString() : null,
                    body.GetProperty("optOfficeId").GetGuid());
                return Json(201, id);
            }));

        // La búsqueda de la carga de proveedores, con la forma de `POST /v1/companies/match`: por
        // sitio web y, si no, por nombre y país. El CRM de verdad normaliza más (tildes, formas
        // jurídicas); acá alcanza con mayúsculas, bordes y el esquema y el "www." del sitio.
        Server
            .Given(Request.Create().WithPath(CompanyMatchPath).UsingPost())
            .AtPriority(1)
            .RespondWith(Response.Create().WithCallback(request =>
            {
                var body = JsonSerializer.Deserialize<JsonElement>(request.Body ?? "{}");
                var website = Host(body.TryGetProperty("website", out var w) ? w.GetString() : null);
                var name = Folded(body.TryGetProperty("name", out var n) ? n.GetString() : null);
                var country = Folded(body.TryGetProperty("country", out var c) ? c.GetString() : null);

                var match = _companies.FirstOrDefault(pair => website is not null && Host(pair.Value.Website) == website);
                if (match.Value is null && name is not null && country is not null)
                    match = _companies.FirstOrDefault(pair => Folded(pair.Value.Name) == name && Folded(pair.Value.Country) == country);

                return Json(200, new
                {
                    company = match.Value is null
                        ? null
                        : new { id = match.Key, optOfficeId = match.Value.OptOfficeId, optOfficeName = MatchedOfficeName },
                });
            }));

        // El volcado del directorio de empresas, con la forma de `GET /v1/companies/directory`: las
        // seis columnas de `CompanyDirectoryEntryDto`, ordenadas por nombre como en el CRM.
        Server
            .Given(Request.Create().WithPath(CompanyDirectoryPath).UsingGet())
            .AtPriority(1)
            .RespondWith(Response.Create().WithCallback(_ =>
                Json(200, _companies
                    .OrderBy(pair => pair.Value.Name, StringComparer.Ordinal)
                    .Select(pair => new
                    {
                        id = pair.Key,
                        name = pair.Value.Name,
                        country = pair.Value.Country,
                        city = (string?)null,
                        isActive = true,
                        categoryCodes = Array.Empty<string>(),
                    }))));

        // Los correos que ya tienen contacto, con la forma de `POST /v1/contacts/existing-emails`:
        // cada uno tal como llegó, con el id del contacto. El CRM de verdad normaliza como
        // `Contact.cs`; acá alcanza con bordes y mayúsculas.
        Server
            .Given(Request.Create().WithPath(ExistingEmailsPath).UsingPost())
            .AtPriority(1)
            .RespondWith(Response.Create().WithCallback(request =>
            {
                var emails = JsonSerializer.Deserialize<JsonElement>(request.Body ?? "{}").GetProperty("emails");

                return Json(200, emails.EnumerateArray()
                    .Select(email => email.GetString()!)
                    .Where(email => _contactEmails.ContainsKey(Folded(email)!))
                    .Select(email => new { email, contactId = _contactEmails[Folded(email)!] }));
            }));

        // Afiliar dos veces el mismo contacto a la misma empresa choca con el índice único del CRM.
        Server
            .Given(Request.Create().WithPath(AffiliationPattern).UsingPost())
            .AtPriority(1)
            .RespondWith(Response.Create().WithCallback(request =>
            {
                var companyId = CompanyIdFromPath(request);
                var contactId = JsonSerializer.Deserialize<JsonElement>(request.Body ?? "{}").GetProperty("contactId").GetGuid();

                return _affiliations.TryAdd((companyId, contactId), 0)
                    ? Json(201, Guid.NewGuid())
                    : Json(409, new { status = 409, detail = "Conflicto de datos.", code = DbConflictCode });
            }));

        // El listado de cuentas de una empresa, enmascaradas, como BankAccountDto del CRM.
        Server
            .Given(Request.Create().WithPath(BankAccountPattern).UsingGet())
            .AtPriority(1)
            .RespondWith(Response.Create().WithCallback(request =>
            {
                var companyId = CompanyIdFromPath(request);
                return Json(200, _companiesWithAccount.ContainsKey(companyId)
                    ? new object[] { new { id = Guid.NewGuid(), companyId, accountNumberMasked = "****1332", isPrincipal = true } }
                    : []);
            }));

        // El alta de una cuenta contesta la cuenta ya enmascarada, como BankAccountDto del CRM.
        Server
            .Given(Request.Create().WithPath(BankAccountPattern).UsingPost())
            .AtPriority(1)
            .RespondWith(Response.Create().WithCallback(request =>
            {
                var body = JsonSerializer.Deserialize<JsonElement>(request.Body ?? "{}");
                var number = body.GetProperty("accountNumber").GetString()!.Replace(" ", "", StringComparison.Ordinal);
                var companyId = CompanyIdFromPath(request);
                _companiesWithAccount[companyId] = 0;
                return Json(201, new
                {
                    id = Guid.NewGuid(),
                    companyId,
                    accountNumberMasked = "****" + number[^4..],
                    isPrincipal = true,
                });
            }));

        Server
            .Given(Request.Create().WithPath(OriginSourcesPath).UsingGet())
            .AtPriority(1)
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new[]
                {
                    new { id = Guid.NewGuid(), code = "feria", name = "Feria" },
                    new { id = SupplierIntakeOriginSourceId, code = "carga-proveedores-opt", name = SupplierIntakeOriginSourceName },
                }));
    }

    /// <summary>La empresa que nombra una ruta <c>/v1/companies/{id}/...</c>.</summary>
    private static Guid CompanyIdFromPath(IRequestMessage request) => Guid.Parse(request.Path.Split('/')[^2]);

    /// <summary>El host de un sitio web sin esquema, sin "www." y sin ruta, o <c>null</c>.</summary>
    private static string? Host(string? website)
    {
        if (string.IsNullOrWhiteSpace(website))
            return null;

        var host = website.Trim().ToLowerInvariant();
        host = host.Contains("://", StringComparison.Ordinal) ? host[(host.IndexOf("://", StringComparison.Ordinal) + 3)..] : host;
        host = host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
        return host.Split('/')[0];
    }

    /// <summary>Un texto sin bordes y en minúsculas, o <c>null</c> si viene en blanco.</summary>
    private static string? Folded(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim().ToLowerInvariant();

    /// <summary>Las peticiones de este test, de la más vieja a la más nueva.</summary>
    private IEnumerable<IRequestMessage> OrderedRequests() =>
        RequestsSinceReset
            .OrderBy(entry => entry.RequestMessage!.DateTime)
            .Select(entry => entry.RequestMessage!);

    /// <summary>
    /// El alta devuelve una identidad nueva por llamada, como hace el CRM de verdad, hasta el
    /// corte que fije <see cref="FailContactCreateAfter"/>. Un id fijo haría que dos filas nuevas
    /// acabaran en un solo destinatario y escondería cualquier fallo de conteo.
    /// </summary>
    private void ServeContactCreate() =>
        Server
            .Given(Request.Create().WithPath(ContactsPath).UsingPost())
            .AtPriority(1)
            .RespondWith(Response.Create().WithCallback(_ =>
            {
                if (Volatile.Read(ref _successfulContactCreates) >= Volatile.Read(ref _failContactCreateAfter))
                    return Json(503, new { detail = "El CRM no está disponible." });

                Interlocked.Increment(ref _successfulContactCreates);
                return Json(201, Guid.NewGuid());
            }));

    /// <summary>
    /// El CRM crea bien las primeras <paramref name="successfulCreates"/> y a partir de ahí deja
    /// de contestar. Es como se prueba que una confirmación se para, informa cuántas filas
    /// completó, y un reintento sigue por donde iba sin crear a nadie dos veces.
    /// <para>
    /// No reprograma nada: mueve el corte que ya mira el alta, así que curarlo con
    /// <see cref="HealContactCreate"/> no pisa la cuenta de lo que ya se creó.
    /// </para>
    /// </summary>
    public void FailContactCreateAfter(int successfulCreates) =>
        Volatile.Write(ref _failContactCreateAfter, successfulCreates);

    /// <summary>El CRM vuelve a crear contactos, sin olvidar los que ya creó.</summary>
    public void HealContactCreate() =>
        Volatile.Write(ref _failContactCreateAfter, int.MaxValue);

    /// <summary>
    /// El alta choca con una dirección que ya existe: 409 con el contacto vivo, que es como
    /// convocatorias descubre el id de alguien que ya estaba. La forma es la del CRM real, un
    /// ProblemDetails con la ficha en la extensión <c>existingContact</c>.
    /// </summary>
    public void ServeContactCreateConflict(StubContact existing) =>
        Server
            .Given(Request.Create().WithPath(ContactsPath).UsingPost())
            // Por delante del alta que ya sirve `ServeContacts`: se añade encima en vez de
            // reprogramar el servidor, así que quien decide entre las dos es la precedencia.
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(409)
                .WithBodyAsJson(new
                {
                    status = 409,
                    detail = "Ya existe un contacto activo con esta dirección de correo.",
                    code = DuplicateEmailCode,
                    existingContact = ToItem(existing),
                }));

    /// <summary>
    /// Como <see cref="ServeContactCreateConflict"/>, pero solo para el alta que trae la dirección
    /// de <paramref name="existing"/>: las demás altas del mismo envío siguen creando contactos.
    /// </summary>
    public void ServeContactCreateConflictFor(StubContact existing) =>
        Server
            .Given(Request.Create()
                .WithPath(ContactsPath)
                .UsingPost()
                .WithBody(body => body is not null && body.Contains(existing.Email, StringComparison.OrdinalIgnoreCase)))
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(409)
                .WithBodyAsJson(new
                {
                    status = 409,
                    detail = "Ya existe un contacto activo con esta dirección de correo.",
                    code = DuplicateEmailCode,
                    existingContact = ToItem(existing),
                }));

    /// <summary>Los ids del filtro <c>id=</c>, repetido una vez por contacto; vacío si no vino.</summary>
    private static HashSet<Guid> QueryIds(IRequestMessage request) =>
        request.Query is not null && request.Query.TryGetValue("id", out var values)
            ? [.. values.Select(Guid.Parse)]
            : [];

    /// <summary>El valor de un parámetro de consulta, o <c>null</c> si no vino.</summary>
    private static string? Query(IRequestMessage request, string name) =>
        request.Query is not null
        && request.Query.TryGetValue(name, out var values)
        && values.Count > 0
            ? values[0]
            : null;

    private static object Page(IReadOnlyCollection<StubContact> contacts) => new
    {
        items = contacts.Select(ToItem),
        totalCount = contacts.Count,
        page = 1,
        pageSize = 100,
    };

    private static object ToItem(StubContact contact) => new
    {
        id = contact.Id,
        firstName = contact.FirstName,
        lastName = contact.LastName,
        name = $"{contact.FirstName} {contact.LastName}",
        email = contact.Email,
        phone = (string?)null,
        canBeEmailed = contact.CanBeEmailed,
        suppressionReason = contact.SuppressionReason,
        companies = Array.Empty<object>(),
        classifications = Array.Empty<object>(),
    };
}

/// <summary>
/// El cuerpo con el que convocatorias le pide al CRM que cree un contacto, tal como llegó. Los
/// dos últimos campos son el registro de consentimiento, que es lo que separa una inscripción
/// propia de una carga hecha por un empleado (spec <c>06</c> §4.2).
/// </summary>
/// <param name="FirstName">Nombre.</param>
/// <param name="LastName">Apellidos.</param>
/// <param name="Email">La dirección.</param>
/// <param name="Phone">Teléfono, si lo escribió.</param>
/// <param name="HasConsent">Si se declara que consintió.</param>
/// <param name="ConsentSource">De dónde sale ese consentimiento.</param>
public sealed record CreatedContactBody(
    string? FirstName,
    string? LastName,
    string? Email,
    string? Phone,
    bool HasConsent,
    string? ConsentSource);

/// <summary>
/// Un contacto del CRM simulado. <c>CanBeEmailed</c> en <c>false</c> reproduce a quien se
/// dio de baja o está marcado como no invitable: sigue viniendo en el listado, y
/// convocatorias lo excluye a la vista.
/// </summary>
public sealed record StubContact(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    bool CanBeEmailed,
    string? SuppressionReason);

/// <summary>Una persona del personal de una oficina simulada.</summary>
/// <remarks><c>FromHr</c> en <c>false</c> es alguien cargado a mano en el directorio y no desde Recursos Humanos.</remarks>
public sealed record StubOptStaff(Guid Id, string FullName, bool IsActive = true, bool FromHr = true);

/// <summary>Una empresa que el CRM del test tiene, con lo que mira la búsqueda de la carga de proveedores.</summary>
/// <param name="Name">El nombre.</param>
/// <param name="Country">El país.</param>
/// <param name="Website">El sitio web, o nulo.</param>
/// <param name="OptOfficeId">La oficina dueña.</param>
public sealed record StubCompany(string Name, string Country, string? Website, Guid OptOfficeId);
