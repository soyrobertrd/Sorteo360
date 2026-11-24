using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.CompanyDirectory;

/// <summary>
/// El directorio de empresas visto desde este servicio. El CRM es su única autoridad, pero nada en
/// esta firma lo dice: ni una ruta, ni un DTO de red, ni un código HTTP. Que el directorio llegue
/// por la red es un hecho de Infrastructure, y por eso los tests de dominio lo sustituyen sin
/// levantar nada.
/// <para>
/// Está modelado igual que <c>IOptDirectory</c> y por la misma razón: hay una sola forma de
/// consumir un directorio del CRM, así que hay una sola que aprender y mantener (ADR-0015).
/// </para>
/// <para>
/// Las dos operaciones existen por separado porque responden a preguntas distintas.
/// <see cref="ResolveAsync"/> es para mostrar: sirve de una copia en caché, tolera que el CRM esté
/// caído y nunca falla. <see cref="GetCompanyAsync"/> es para escribir: consulta en vivo, porque una
/// empresa registrada hace dos minutos tiene que poder usarse, y falla si el CRM no contesta.
/// </para>
/// </summary>
public interface ICompanyDirectory
{
    /// <summary>
    /// Resuelve la identidad de las empresas indicadas desde la copia en caché del directorio,
    /// informando en <see cref="CompanyLookup.Status"/> qué tan vigente es esa copia.
    /// <para>
    /// No devuelve <c>Result</c> a propósito: esta operación no tiene forma de fallar. Si el CRM no
    /// responde, sirve la última copia buena; si nunca hubo una, devuelve la resolución vacía. La
    /// ficha de un proveedor jamás se cae por un nombre para mostrar.
    /// </para>
    /// </summary>
    Task<CompanyLookup> ResolveAsync(
        IReadOnlyCollection<Guid> companyIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Consulta una empresa en vivo contra el CRM, incluidas la que está dormida y la que se dio
    /// de baja: la ficha de un proveedor tiene que seguir editándose años después, así que lo
    /// único que esta consulta distingue es si el id existió alguna vez.
    /// <para>
    /// En vivo y nunca desde una copia en caché, porque una empresa registrada hace dos minutos
    /// tiene que poder usarse. Devuelve <c>None</c> cuando el id no existe, y falla con
    /// <see cref="ErrorKind.Unavailable"/> cuando el CRM no contesta — nunca se confunde una cosa
    /// con la otra, porque aceptar un id sin validar dejaría en la base justamente el dato malo
    /// que esta validación existe para impedir.
    /// </para>
    /// </summary>
    Task<Result<Maybe<CompanyInfo>, BaseError>> GetCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken);
}
