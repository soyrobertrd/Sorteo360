using System.Globalization;
using Sorteo360.Convocatorias.Core.Shared;

namespace Sorteo360.Convocatorias.Core.CheckIns;

/// <summary>
/// La caja de búsqueda de la lista de acceso (spec <c>09</c> §5.1, enmendada el 2026-10-10). Dos
/// ramas unidas por un O, sin adivinar si lo tecleado parece una dirección:
/// <list type="bullet">
/// <item>cada palabra tiene que estar en el nombre, el apellido o la referencia, cada una en el
/// campo que sea: <c>garcía maría</c> y <c>garcía 00427</c> la encuentran;</item>
/// <item>lo tecleado entero, normalizado, es exactamente la dirección. Un pedazo de dirección no
/// encuentra nada, así que la caja no sirve para sacar direcciones letra a letra.</item>
/// </list>
/// </summary>
public sealed class DoorListSearch
{
    /// <summary>
    /// Mayúsculas y tildes aparte, como <c>ParticipantFilter</c>: <c>garcia</c> encuentra a
    /// <i>García</i> y <c>nunez</c> a <i>Núñez</i>. Depende de ICU, que trae la imagen
    /// <c>aspnet:10.0</c> de Debian; con globalización invariante se volvería ordinal sin avisar, y
    /// <c>DoorListSearchTests</c> es lo que avisa.
    /// </summary>
    private const CompareOptions Comparison = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    private readonly string[] _words;
    private readonly string _email;

    public DoorListSearch(string? input)
    {
        _words = input?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries) ?? [];
        _email = _words.Length == 0 ? string.Empty : EmailAddressShape.Normalize(input!);
    }

    /// <summary>Si la fila pasa la búsqueda. Una búsqueda vacía deja pasar todas.</summary>
    public bool Matches(DoorListRow row)
    {
        if (_words.Length == 0)
            return true;

        if (row.EmailNormalized == _email)
            return true;

        var text = CultureInfo.InvariantCulture.CompareInfo;

        return _words.All(word =>
            text.IndexOf(row.FirstName, word, Comparison) >= 0
            || text.IndexOf(row.LastName, word, Comparison) >= 0
            || text.IndexOf(row.Reference, word, Comparison) >= 0);
    }
}
