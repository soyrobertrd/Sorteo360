using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Shared;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Errors;

/// <summary>
/// Fallos de las reglas propias del evento.
/// <para>
/// El presupuesto estimado llega del formulario como monto y moneda, y sus dos rechazos son
/// <b>petición inválida</b>: a diferencia de los ids de OPT, acá nada pudo cambiar entre que se
/// cargó la pantalla y se envió, así que un valor fuera de rango solo puede venir de un cliente
/// que armó mal el payload. Cada uno lleva código propio porque el formulario tiene que poder
/// marcar el campo equivocado sin leer la prosa.
/// </para>
/// </summary>
public static class EventErrors
{
    /// <summary>El evento no está en in_progress, el único estado desde el que se cierra.</summary>
    /// <remarks>
    /// Un evento en planning nunca comenzó y uno terminal ya no admite transiciones. El servicio
    /// lo comprueba para dar el mensaje, y el repositorio lo vuelve a comprobar bajo el bloqueo del
    /// evento para cerrar la carrera.
    /// </remarks>
    public static BaseError NotClosable() => new(
        "El evento solo puede cerrarse desde el estado in_progress.",
        "EVENT_NOT_CLOSABLE",
        ErrorKind.Conflict);

    /// <summary>
    /// El evento está cerrado o cancelado, y un evento terminal ya no admite cambios: ni en sus
    /// propios campos ni en sus ciudades.
    /// </summary>
    public static BaseError NotEditable() => new(
        "El evento ya está cerrado o cancelado.",
        "EVENT_NOT_EDITABLE",
        ErrorKind.Conflict);

    /// <summary>
    /// La moneda del presupuesto no pertenece al conjunto cerrado que la OPT opera. Se decide en
    /// el borde y no en el dominio porque <c>Currency</c> es un enum cerrado: para cuando el valor
    /// llega a la entidad ya no existe una moneda inválida que rechazar.
    /// </summary>
    public static BaseError BudgetCurrencyNotSupported() => new(
        "La moneda del presupuesto estimado debe ser USD o EUR.",
        "EVENT_BUDGET_CURRENCY_NOT_SUPPORTED",
        ErrorKind.Invalid);

    /// <summary>
    /// El monto del presupuesto no es positivo. Un presupuesto de cero no es un presupuesto: para
    /// decir que todavía no hay uno, se omite el campo.
    /// </summary>
    public static BaseError BudgetAmountInvalid() => new(
        "El monto del presupuesto estimado debe ser mayor que cero.",
        "EVENT_BUDGET_AMOUNT_INVALID",
        ErrorKind.Invalid);

    /// <summary>
    /// La edición llegó sin idioma. Este PATCH reemplaza la representación editable completa, así
    /// que omitirlo borraría el valor guardado, y un evento sin idioma no tiene plantilla con la
    /// que convocar: se rechaza en vez de ignorar el nulo, que haría a este campo comportarse
    /// distinto de todos los demás del mismo endpoint (spec de convocatorias <c>01</c> §2).
    /// <para>
    /// Va con código propio y no por el validador porque el filtro de validación del chasis
    /// responde <c>ValidationProblem</c>, que no lleva <c>code</c>, y el contrato exige uno.
    /// </para>
    /// </summary>
    public static BaseError LanguageRequired() => new(
        "El evento requiere un idioma.",
        "EVENT_LANGUAGE_REQUIRED",
        ErrorKind.Invalid);

    /// <summary>La zona horaria de la ciudad no es un id IANA que el sistema conozca.</summary>
    /// <remarks>
    /// Un id de Windows también se rechaza aunque .NET sepa traducirlo, porque el contrato admite un
    /// solo formato. Lleva código propio y sale del borde, como el presupuesto, no del validador,
    /// porque las tres rutas que reciben la zona tienen que poder marcar el campo sin leer la prosa.
    /// </remarks>
    public static BaseError TimeZoneUnknown() => new(
        "La zona horaria debe ser un identificador IANA, como America/Santo_Domingo.",
        "TIME_ZONE_UNKNOWN",
        ErrorKind.Invalid);

    /// <summary>
    /// El campo de orden pedido no es uno de los que el listado sabe ordenar. Se rechaza en vez
    /// de ignorarse: devolver la página en un orden distinto del pedido, sin decirlo, es peor que
    /// negarse. Lleva código propio para que la tabla sepa que el problema es la columna y no la
    /// paginación.
    /// </summary>
    public static BaseError SortFieldNotSupported() => new(
        $"El campo de orden debe ser uno de: {string.Join(", ", Enum.GetNames<EventSortField>().Select(n => char.ToLowerInvariant(n[0]) + n[1..]))}.",
        "EVENT_SORT_FIELD_NOT_SUPPORTED",
        ErrorKind.Invalid);

    /// <summary>
    /// El sentido del orden no es <c>asc</c> ni <c>desc</c>. Va con código propio y no con el del
    /// campo porque son dos parámetros distintos y quien consume marca uno u otro.
    /// </summary>
    public static BaseError SortOrderNotSupported() => new(
        $"El sentido del orden debe ser uno de: {string.Join(", ", Enum.GetNames<SortOrder>().Select(n => n.ToLowerInvariant()))}.",
        "EVENT_SORT_ORDER_NOT_SUPPORTED",
        ErrorKind.Invalid);
}
