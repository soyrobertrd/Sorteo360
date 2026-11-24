namespace Sorteo360.Events.Core.CrmDirectory;

/// <summary>
/// Lo que el CRM informó durante esta petición. Quien resuelve nombres lo escribe y el borde lo lee
/// para emitir las cabeceras de estado, de modo que ningún handler tenga que acordarse de
/// propagarlas.
/// <para>
/// Es uno solo para todo lo que se resuelve contra el CRM, no uno por directorio: una respuesta
/// tiene un único grado de confianza, y repartirlo en varios obligaría al cliente a combinarlos.
/// </para>
/// <para>
/// Registrado por petición (scoped). <see cref="Status"/> queda en <c>null</c> mientras nadie
/// resuelva nada: una petición que no toca el CRM no lleva cabecera.
/// </para>
/// </summary>
public sealed class CrmDirectoryStatusContext
{
    /// <summary>El peor estado informado en esta petición, o <c>null</c> si no hubo ninguno.</summary>
    public CrmDirectoryStatus? Status { get; private set; }

    /// <summary>
    /// Informa el resultado de una resolución. Se queda con el peor de la petición: si algo se
    /// sirvió degradado, la respuesta entera lo es, aunque otra parte se haya resuelto bien.
    /// </summary>
    public void Report(CrmDirectoryStatus status)
    {
        if (Status is null || status > Status)
            Status = status;
    }
}
