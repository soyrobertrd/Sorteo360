namespace Sorteo360.Convocatorias.Infrastructure.Crm;

/// <summary>
/// Una página de contactos tal como la devuelve <c>GET /v1/contacts</c> del CRM.
/// </summary>
/// <param name="Items">Los contactos de esta página.</param>
/// <param name="TotalCount">Cuántos casan con el filtro en total.</param>
/// <param name="Page">Página devuelta, 1-based.</param>
/// <param name="PageSize">Tamaño de página que aplicó el CRM, topado por él en 100.</param>
public sealed record CrmContactPageResponse(
    List<CrmContactResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// Un contacto del CRM. Es a propósito un subconjunto de lo que el CRM emite: el teléfono, las
/// fechas, las clasificaciones y las empresas llegan y se descartan, porque la selección no los
/// usa y el nombre de una empresa nunca se copia a la fila de un destinatario.
/// <para>
/// <c>canBeEmailed</c> y <c>suppressionReason</c> los calcula el CRM y son la respuesta entera:
/// este servicio no vuelve a razonar sobre el correo vacío ni sobre las bajas, o la regla
/// acabaría viviendo en dos bases de código (spec <c>08</c> §2.6).
/// </para>
/// </summary>
/// <param name="Id">Identidad del contacto en el CRM.</param>
/// <param name="FirstName">Nombre.</param>
/// <param name="LastName">Apellidos.</param>
/// <param name="Email">Dirección, nula si no tiene.</param>
/// <param name="CanBeEmailed">Si se le puede escribir, según el CRM.</param>
/// <param name="SuppressionReason">Su código de motivo, nulo cuando sí se le puede escribir.</param>
public sealed record CrmContactResponse(
    Guid Id,
    string? FirstName,
    string? LastName,
    string? Email,
    bool CanBeEmailed,
    string? SuppressionReason);

/// <summary>
/// Una lista de contactos tal como la devuelve <c>GET /v1/contact-lists/{id}</c> del CRM. Es un
/// subconjunto a propósito: el nombre, las cuentas y la auditoría llegan y se descartan, porque
/// la selección solo necesita de quién es y quiénes la forman.
/// </summary>
/// <param name="Id">Identidad de la lista.</param>
/// <param name="OptOfficeId">La OPT dueña.</param>
/// <param name="ContactIds">Sus miembros vivos, por orden de alta en la lista.</param>
public sealed record CrmContactListResponse(
    Guid Id,
    Guid OptOfficeId,
    List<Guid>? ContactIds);

/// <summary>
/// Una oficina tal como la devuelven <c>GET /v1/opts/{id}</c> y cada fila de <c>GET /v1/opts</c>
/// del CRM, reducida al código, al nombre, a si sigue abierta, a su dirección de envío y a su
/// personal. La dirección postal y el contacto llegan y se descartan.
/// </summary>
/// <param name="Id">Identidad de la oficina.</param>
/// <param name="Code">Código, el del atributo <c>opt_code</c> de Keycloak.</param>
/// <param name="IsActive">Falso si la oficina está cerrada.</param>
/// <param name="SenderEmail">Desde dónde salen sus campañas; nula si no tiene una.</param>
/// <param name="Name">El nombre que se muestra, como <c>OPT Madrid</c>.</param>
/// <param name="Staff">Su personal; solo lo trae la lectura por id, el listado no.</param>
public sealed record CrmOptOfficeResponse(
    Guid Id,
    string? Code,
    bool IsActive,
    string? SenderEmail = null,
    string? Name = null,
    List<CrmOptStaffResponse>? Staff = null);

/// <summary>Una persona del personal de una oficina, tal como viene en <c>staff</c> de <c>GET /v1/opts/{id}</c>.</summary>
/// <remarks>
/// El cargo, el correo, el teléfono y el colaborador llegan y se descartan: la carga de
/// proveedores solo muestra el nombre de quien firma.
/// </remarks>
/// <param name="Id">Identidad de la persona.</param>
/// <param name="FullName">Su nombre completo.</param>
/// <param name="IsActive">Falso si ya no está en la oficina.</param>
/// <param name="FromHr">Cierto si la cargó Recursos Humanos.</param>
public sealed record CrmOptStaffResponse(
    Guid Id,
    string? FullName,
    bool IsActive,
    bool FromHr);
