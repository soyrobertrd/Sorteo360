using System.Text.RegularExpressions;

namespace Sorteo360.Convocatorias.Core.Shared;

/// <summary>
/// Si un teléfono es uno que el CRM acepta en el alta de un contacto (PGTI-355).
/// </summary>
/// <remarks>
/// Es copia de <c>PhoneRules</c> de crm-service, que es el dueño de la regla: dígitos, espacios y
/// <c>+ - ( )</c>, de 7 a 20 caracteres, o en blanco; si allá cambia, cambia aquí. Hace falta
/// tenerla de este lado porque un 400 del CRM se lee como "el CRM no está": una inscripción se
/// quedaría pendiente para siempre y una fila de una carga fallaría en cada reintento. El portal la
/// usa para pedirle a la persona que corrija el teléfono, y el alta en el CRM para no mandar el
/// teléfono de una hoja que nadie puede corregir.
/// </remarks>
public static partial class PhoneShape
{
    /// <summary>El patrón del CRM; <c>\z</c> y no <c>$</c>, que dejaría pasar un salto de línea final.</summary>
    [GeneratedRegex(@"^[0-9 +\-()]{7,20}\z")]
    private static partial Regex Pattern();

    /// <summary>En blanco, ausente o con la forma que el CRM acepta.</summary>
    public static bool IsAcceptedByCrm(string? value) =>
        string.IsNullOrWhiteSpace(value) || Pattern().IsMatch(value);
}
