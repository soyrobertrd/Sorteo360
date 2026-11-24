namespace Sorteo360.Events.Infrastructure.Crm;

/// <summary>
/// La lectura de empresas del CRM que este servicio consume. Es uno de los dos únicos lugares donde
/// aparece una ruta del CRM —el otro es <see cref="ICrmOptClient"/>—: el chasis sabe autenticarse y
/// sobrevivir a la red, pero no conoce ninguna ruta ni ningún DTO, y el dominio no sabe siquiera que
/// hay una red.
/// <para>
/// Todo fallo de red, de autenticación o de estado sale como <see cref="HttpRequestException"/>,
/// que es lo que el chasis garantiza. Quien llama lo traduce a un <c>Result</c>; acá no hay
/// política de degradación, solo transporte.
/// </para>
/// </summary>
public interface ICrmCompanyClient
{
    /// <summary>
    /// Trae el volcado completo del directorio de empresas: la identidad mínima de todas, sin
    /// paginar y sin ocultar nada —la dormida y la dada de baja vienen marcadas, no omitidas—,
    /// porque un proveedor contratado hace tres años tiene que seguir mostrando su empresa.
    /// <para>
    /// Es una sola llamada para dibujar una pantalla entera: resolver fila por fila costaría una
    /// petición por proveedor.
    /// </para>
    /// </summary>
    Task<List<CrmCompanyDirectoryEntryResponse>> ListCompaniesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Resuelve una empresa por id, incluidas la dormida y la dada de baja: es la lectura para
    /// mostrar, y una empresa cerrada tiene que seguir resolviendo en los proveedores que la
    /// registraron. Devuelve <c>null</c> solo si el id nunca existió.
    /// </summary>
    Task<CrmCompanyResponse?> GetCompanyAsync(Guid companyId, CancellationToken cancellationToken);

    /// <summary>
    /// Trae los contactos vigentes de una empresa. Es la única ruta del CRM que este servicio pide
    /// por pantalla y no por copia: el volcado del directorio no trae contactos a propósito, así que
    /// la ficha del proveedor —la única que los muestra— los consulta cuando se dibuja.
    /// <para>
    /// Trae como máximo <see cref="CrmCompanyClient"/>.<c>ContactPageSize</c> contactos y no encadena
    /// páginas: la ficha de un proveedor no es la pantalla que resuelve una empresa con más de cien
    /// personas registradas, y encadenar metería varias idas al CRM dentro de una lectura que ya
    /// depende de que conteste.
    /// </para>
    /// <para>
    /// Devuelve <c>null</c> ante un 404. La ruta del CRM lo declara, pero su lectura de hoy no lo
    /// emite: consulta los contactos de ese id sin comprobar antes que la empresa exista, así que una
    /// empresa desconocida contesta la página vacía igual que una sin nadie registrado. La rama se
    /// conserva porque el 404 está en el contrato y porque, si algún día llega, tiene que
    /// distinguirse de que el CRM no conteste.
    /// </para>
    /// </summary>
    Task<CrmCompanyContactPageResponse?> GetCompanyContactsAsync(
        Guid companyId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lee una cuenta bancaria de una empresa, incluida la desactivada. Devuelve <c>null</c> ante un
    /// 404, que el CRM contesta igual si la cuenta es de otra empresa que si nunca existió.
    /// </summary>
    Task<CrmBankAccountResponse?> GetBankAccountAsync(
        Guid companyId,
        Guid bankAccountId,
        CancellationToken cancellationToken);
}
