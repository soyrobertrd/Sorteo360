namespace Sorteo360.Events.Infrastructure.Crm;

/// <summary>
/// Una empresa tal como la devuelve la lectura por id del CRM.
/// <para>
/// Es a propósito un subconjunto de lo que el CRM emite: el domicilio, el teléfono, las categorías
/// y el resto llegan y se descartan. Lo que se conserva es lo que esta respuesta responde en sus dos
/// usos: validar que el id exista al escribir, y rellenar la identidad de una empresa que el volcado
/// del directorio no trae —la cola de empresas cerradas—. Deserializar de menos no cuesta nada y
/// evita que este servicio dependa de campos que no necesita.
/// </para>
/// <para>
/// <c>IsActive</c> e <c>IsDeleted</c> son dos ejes distintos —la relación comercial y la baja del
/// registro— y esta lectura puede devolver cualquier combinación de los dos: es la lectura para
/// mostrar y para validar, no el desplegable de quien elige.
/// </para>
/// </summary>
public sealed record CrmCompanyResponse(
    Guid Id,
    string Name,
    string Country,
    string? City,
    bool IsActive,
    bool IsDeleted);

/// <summary>
/// Una empresa tal como la devuelve el volcado del directorio del CRM: la identidad mínima con la
/// que este servicio muestra, filtra y ordena sin guardar ni un nombre propio.
/// <para>
/// <c>IsActive</c> aquí es <b>un solo bit</b> —si la empresa todavía se puede elegir— y vale
/// <c>false</c> tanto para una dormida como para una dada de baja. Los dos ejes separados están en
/// <see cref="CrmCompanyResponse"/>, que es la lectura por id con la que se valida una escritura.
/// </para>
/// </summary>
public sealed record CrmCompanyDirectoryEntryResponse(
    Guid Id,
    string Name,
    string Country,
    string? City,
    bool IsActive);

/// <summary>
/// Una página de contactos de empresa tal como la devuelve el CRM. De la envoltura paginada solo se
/// conservan los elementos: esta lectura pide la página máxima de una empresa y no encadena, así
/// que el total y el número de página no responden nada que esta pantalla pregunte.
/// </summary>
public sealed record CrmCompanyContactPageResponse(List<CrmCompanyContactResponse> Items);

/// <summary>
/// Un contacto de empresa del CRM. Es a propósito un subconjunto de lo que aquel emite: el id de la
/// relación, el departamento, sus fechas y el eje de consentimiento llegan y se descartan.
/// <para>
/// <c>CanBeEmailed</c> se descarta y no se conserva porque esta ficha no filtra por él: quien retiró
/// su consentimiento de mercadeo aparece igual, y guardar el campo solo invitaría a que alguien lo
/// usara para esconderlo (ADR-0015).
/// </para>
/// </summary>
public sealed record CrmCompanyContactResponse(
    Guid ContactId,
    string Name,
    string? Email,
    string? Phone,
    string? JobTitle);

/// <summary>
/// Una cuenta bancaria de empresa tal como la lee el CRM. Es a propósito un subconjunto: de todo lo
/// que aquel emite —enmascarado siempre— solo se conserva lo que la validación mira, y nada del
/// número ni del banco llega a guardarse acá (ADR-0028).
/// </summary>
public sealed record CrmBankAccountResponse(Guid Id, Guid CompanyId, DateTimeOffset? DeactivatedAt);
