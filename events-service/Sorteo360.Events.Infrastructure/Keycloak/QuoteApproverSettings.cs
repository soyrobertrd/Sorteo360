using System.ComponentModel.DataAnnotations;

namespace Sorteo360.Events.Infrastructure.Keycloak;

/// <summary>
/// Ajustes de resolución de operadores contra el realm (<c>QuoteApprovers__*</c>).
/// <para>
/// <b>Son dos vigencias y no una</b>, porque las dos cosas que se cachean no envejecen igual. El
/// grafo de roles solo cambia cuando alguien edita el realm en la consola, cosa que pasa en meses;
/// quién tiene un rol cambia cuando alguien entra o sale de la organización. Una sola vigencia obligaría a usar la corta para todo y a recorrer cada pocos minutos un
/// grafo que no se mueve.
/// </para>
/// <para>
/// <b>No hay forma de invalidar nada.</b> ADR-0016 le da la propiedad del realm a la consola, así
/// que una edición de roles no pasa por ningún despliegue y nada puede avisarnos. Un cambio en el
/// realm tarda como mucho una vigencia en verse, y ese es el costo aceptado.
/// </para>
/// </summary>
public sealed class QuoteApproverSettings
{
    public static string SectionName => "QuoteApprovers";

    /// <summary>El realm donde se busca. Sale de la configuración y no del <c>Authority</c>, que en
    /// desarrollo es la URL de cara al navegador y no la de la red interna.</summary>
    [Required]
    public string Realm { get; set; } = "sorteo360";

    /// <summary>
    /// Cuántos minutos se considera vigente el grafo de roles invertido. Largo a propósito: son
    /// horas, porque solo cambia con una edición manual del realm.
    /// <para>
    /// Cero es válido y significa recorrerlo en cada lectura; negativo no, y por eso se valida al
    /// arrancar.
    /// </para>
    /// </summary>
    [Range(0, int.MaxValue)]
    public int RoleGraphTtlMinutes { get; set; } = 360;

    /// <summary>
    /// Cuántos minutos se considera vigente quién tiene los roles y quién lleva cada
    /// <c>opt_code</c>. Corto: son minutos, porque una persona entra o sale.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int MembershipTtlMinutes { get; set; } = 5;

    /// <summary>
    /// Cuántos registros se piden por página. Los dos endpoints de Keycloak devuelven 100 por
    /// defecto y no existe ninguno que diga cuántos hay en total, así que la paginación es
    /// explícita y este es su tamaño.
    /// </summary>
    [Range(1, 1000)]
    public int PageSize { get; set; } = 100;

    /// <summary>Antigüedad a partir de la cual toca volver a recorrer el grafo de roles.</summary>
    public TimeSpan RoleGraphTtl => TimeSpan.FromMinutes(RoleGraphTtlMinutes);

    /// <summary>Antigüedad a partir de la cual toca volver a preguntar quién es quién.</summary>
    public TimeSpan MembershipTtl => TimeSpan.FromMinutes(MembershipTtlMinutes);
}
