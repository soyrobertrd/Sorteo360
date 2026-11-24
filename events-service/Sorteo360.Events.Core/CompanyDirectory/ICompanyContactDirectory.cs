using CSharpFunctionalExtensions;

namespace Sorteo360.Events.Core.CompanyDirectory;

/// <summary>
/// A quién llamar de una empresa del CRM. Como en <see cref="ICompanyDirectory"/>, nada en esta
/// firma dice que el CRM esté al otro lado de una red: eso es un hecho de Infrastructure.
/// <para>
/// Vive aparte del directorio, y no como una tercera operación suya, porque no comparte lo que hace
/// a aquel un directorio: no hay copia completa que servir ni renovación que decidir. Es una
/// consulta en vivo, de una empresa a la vez, que solo pide la ficha del proveedor —el listado, el
/// pool y el histórico se quedan en la copia—. Juntarlas obligaría a los cuatro dobles de dominio
/// que hoy sustituyen el directorio a cargar con un método que ninguno usa.
/// </para>
/// </summary>
public interface ICompanyContactDirectory
{
    /// <summary>
    /// Los contactos vigentes de una empresa, o <c>None</c> si el CRM no contestó.
    /// <para>
    /// La distinción es el punto entero de esta firma: una lista vacía es "esta empresa no tiene a
    /// nadie registrado" y <c>None</c> es "no se pudo averiguar". Devolver la lista vacía en los dos
    /// casos dejaría una ficha que parece sin contactos sin que nadie se entere, que es justo lo que
    /// pasa cuando hay un evento en curso y alguien necesita un teléfono.
    /// </para>
    /// <para>
    /// No devuelve <c>Result</c>, y por lo mismo que <see cref="ICompanyDirectory.ResolveAsync"/>:
    /// esto es para mostrar, y la ficha nunca se cae por un dato para mostrar. Que el CRM no
    /// contestara se informa en <c>CrmDirectoryStatusContext</c>, que es de donde sale la cabecera
    /// con la que el cliente dibuja un solo aviso de degradación.
    /// </para>
    /// <para>
    /// Devuelve a quien retiró su consentimiento de mercadeo. Llamar a un proveedor por un trabajo ya
    /// contratado no es mercadeo, y el vendedor de un suplidor que se borró de las campañas no puede
    /// quedarse sin teléfono en la ficha de una cotización firmada.
    /// </para>
    /// </summary>
    Task<Maybe<IReadOnlyList<CompanyContact>>> GetContactsAsync(
        Guid companyId,
        CancellationToken cancellationToken);
}
