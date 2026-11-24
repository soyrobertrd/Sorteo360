namespace Sorteo360.Convocatorias.Core.Crm;

/// <summary>
/// Un contacto que convocatorias le pide crear al CRM: desde una fila de hoja confirmada, o
/// desde una inscripción del portal.
/// <para>
/// Se crea <b>suelto</b>, sin empresa, venga de donde venga. Desde una carga, la columna de
/// empresa del archivo se queda como texto en la fila revisada para que una persona la
/// reconcilie después; desde una inscripción, <c>company</c> se queda como texto en la fila del
/// registro por el mismo motivo (spec <c>06</c> §4.1). Crear empresas desde una columna con
/// erratas es lo que el paso de revisión existe para evitar, y deduplicar empresas por dominio
/// está sin construir y tiene reglas propias (spec <c>05</c> §6).
/// </para>
/// <para>
/// <b>Sin <c>onBehalfOfUserId</c>.</b> La spec <c>08</c> §4 lo aplaza, así que todo contacto que
/// salga de acá queda atribuido a <c>service-account-convocatorias-service</c>. El aplazamiento
/// es aceptable porque el lote de la carga registra quién confirmó, y el cambio posterior se
/// rellena desde ese registro.
/// </para>
/// </summary>
/// <param name="FirstName">Nombre.</param>
/// <param name="LastName">Apellidos.</param>
/// <param name="Email">La dirección, cruda: normalizar es del CRM.</param>
/// <param name="Phone">Teléfono, si la hoja lo traía.</param>
/// <param name="ConsentSource">
/// De dónde sale el consentimiento: <see cref="ConsentSources.VerbalUpload"/> desde una carga
/// confirmada y <see cref="ConsentSources.SelfRegistration"/> desde el portal.
/// </param>
public sealed record NewCrmContact(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? ConsentSource);

/// <summary>
/// Los valores de <c>consent_source</c> que el CRM reconoce, en lo que a este servicio le toca.
/// Van acá y no sueltos en cada sitio donde se llama porque los dos llamadores que crean
/// contactos tienen <b>evidencias distintas</b>, y esa diferencia es el dato (spec <c>08</c>
/// §2.2): de un año después depende poder decir por qué esa persona está en la lista.
/// </summary>
public static class ConsentSources
{
    /// <summary>Una carga confirmada: el empleado tomó el consentimiento de palabra.</summary>
    public const string VerbalUpload = "verbal_upload";

    /// <summary>
    /// El portal de registro: la persona escribió sus propios datos (spec <c>06</c> §4.2).
    /// <b>Es la evidencia más fuerte de las dos</b> y por eso no lleva paso de confirmación
    /// humana, mientras que la carga sí: acá el consentimiento no lo declara un tercero.
    /// </summary>
    public const string SelfRegistration = "self_registration";
}
