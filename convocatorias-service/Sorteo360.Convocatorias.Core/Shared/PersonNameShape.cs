using System.Globalization;

namespace Sorteo360.Convocatorias.Core.Shared;

/// <summary>
/// Cuándo dos nombres son el de la misma persona. Lo pregunta una inscripción al compararse con
/// el contacto que ya está en el CRM, y la respuesta decide si esa fila marca
/// <c>contact_details_differ</c> (spec <c>06</c> §4.2).
/// <para>
/// <b>La regla entera es mayúsculas y tildes aparte, sobre el nombre completo recortado.</b> Es
/// lo que dice §4.2 y ni una coma más, y lo de menos no es un descuido: un desconocido tecleando
/// en un móvil no pone tildes y el formulario le pone mayúsculas solo, así que avisar por eso
/// llenaría de ruido la lista que <c>07</c> saca para revisar a mano hasta que nadie la mire.
/// </para>
/// <para>
/// <b>Y ni una regla de más.</b> Juntar los espacios de dentro, aceptar el apellido delante o
/// quitar los títulos son diferencias de verdad, y esconderlas sería decidir por la persona que
/// revisa. Acá no se sabe si "J. Perez" es una corrección o una errata; lo único que se sabe es
/// que alguien tiene que mirarlo.
/// </para>
/// <para>
/// Vive al lado de <see cref="EmailAddressShape"/> y por el mismo motivo: dos copias de "cuándo
/// son la misma persona" acaban separándose, y la que se quede corta deja de avisar.
/// </para>
/// </summary>
public static class PersonNameShape
{
    /// <summary>
    /// Qué se ignora al comparar. <see cref="CompareOptions.IgnoreNonSpace"/> es lo que borra la
    /// tilde y la eñe, que en Unicode son la misma cosa: una marca sin ancho encima de una letra.
    /// </summary>
    private const CompareOptions NameComparison = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    /// <summary>
    /// Si las dos personas son la misma, con el nombre y el apellido por separado, que es como
    /// los guardan las dos partes. <b>Esta es la sobrecarga que se usa</b>; la de dos argumentos
    /// existe para el caso en que el nombre completo ya venga armado.
    /// <para>
    /// <b>Cada mitad se recorta antes de unirlas</b>, y ahí está la diferencia. Recortar solo el
    /// resultado dejaría pasar un espacio de sobra al final del nombre de pila: "Juan " más
    /// "Pérez" da "Juan  Pérez", que discrepa de la ficha que dice exactamente lo mismo. Y la
    /// fila se guarda recortada, así que ese aviso mandaría a revisar a mano a una persona cuyos
    /// datos ya coinciden.
    /// </para>
    /// <para>
    /// Las mitades <b>sí</b> se juntan antes de comparar, porque §4.2 compara el nombre completo
    /// y no campo a campo. Quien mete su segundo apellido en la casilla del nombre se llama
    /// igual, y §4.1 ya dijo que "Lic. Juan Pérez de la Cruz" no tiene punto de corte fiable:
    /// avisar por dónde cayó la raya sería ruido en una lista que alguien revisa entera.
    /// </para>
    /// </summary>
    /// <param name="firstNameLeft">El nombre de una de las dos.</param>
    /// <param name="lastNameLeft">Su apellido.</param>
    /// <param name="firstNameRight">El nombre de la otra.</param>
    /// <param name="lastNameRight">Su apellido.</param>
    public static bool Matches(
        string? firstNameLeft,
        string? lastNameLeft,
        string? firstNameRight,
        string? lastNameRight) =>
        Matches(FullName(firstNameLeft, lastNameLeft), FullName(firstNameRight, lastNameRight));

    /// <summary>
    /// Si los dos nombres completos son el de la misma persona. Nulo y vacío son lo mismo acá: si
    /// ninguno de los dos lados dice nada no hay discrepancia que enseñarle a nadie, y si solo uno
    /// calla sí la hay.
    /// </summary>
    /// <param name="left">Uno de los dos nombres completos.</param>
    /// <param name="right">El otro.</param>
    public static bool Matches(string? left, string? right) =>
        // Invariante y no la cultura del hilo: quién atiende la petición no puede cambiar si dos
        // personas son la misma, y el hilo de una petición web no tiene cultura propia.
        CultureInfo.InvariantCulture.CompareInfo.Compare(
            (left ?? string.Empty).Trim(),
            (right ?? string.Empty).Trim(),
            NameComparison) == 0;

    /// <summary>
    /// El nombre completo: las dos mitades recortadas y unidas por un espacio. Con una sola mitad
    /// no queda ningún espacio suelto, que es lo que haría discrepar a dos fichas igual de mancas.
    /// </summary>
    private static string FullName(string? firstName, string? lastName) =>
        $"{(firstName ?? string.Empty).Trim()} {(lastName ?? string.Empty).Trim()}".Trim();
}
