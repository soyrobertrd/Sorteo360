using System.Text.RegularExpressions;

namespace Sorteo360.Convocatorias.Core.Shared;

/// <summary>Dice si una cuenta bancaria es una que el CRM acepta en su alta (PGTI-358).</summary>
/// <remarks>
/// Es copia de <c>BankAccountRules</c> y de los topes de <c>BankAccount</c> de crm-service, que es el
/// dueño de las reglas (ADR-0029); si allá cambian, cambian aquí. Hace falta tenerlas de este lado
/// por lo mismo que <see cref="PhoneShape"/>: un 400 del CRM se lee como "el CRM no está", y la carga
/// de proveedores le diría a quien envía que lo intente más tarde con un SWIFT que nunca va a pasar.
/// Todo se compara compactado, sin espacios y en mayúsculas, como lo compara el CRM.
/// </remarks>
public static partial class BankAccountShape
{
    /// <summary>El titular cabe en este tope, que es <c>BankAccount.HolderMaxLength</c>.</summary>
    public const int HolderMaxLength = 200;

    /// <summary>La dirección del titular cabe en este tope, que es <c>BankAccount.HolderAddressMaxLength</c>.</summary>
    public const int HolderAddressMaxLength = 500;

    /// <summary>El nombre de un banco, el de la cuenta y el del intermediario, cabe en este tope.</summary>
    public const int BankNameMaxLength = 200;

    /// <summary>Un número de cuenta o un IBAN cabe en este tope, contado sin espacios.</summary>
    public const int AccountNumberMaxLength = 34;

    /// <summary>Estas son las monedas de <c>BankAccountCurrency</c> del CRM.</summary>
    public static IReadOnlySet<string> Currencies { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "USD", "EUR", "DOP" };

    /// <summary>Quita los espacios y pasa a mayúsculas; un valor en blanco es nulo.</summary>
    public static string? Compact(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : WhitespacePattern().Replace(value, "").ToUpperInvariant();

    /// <summary>Dice si el valor es un código ISO 3166 alfa-2.</summary>
    public static bool IsCountry(string? value) => Compact(value) is { } country && CountryPattern().IsMatch(country);

    /// <summary>Dice si el país es Estados Unidos, el único que exige ABA.</summary>
    public static bool IsUnitedStates(string? value) => Compact(value) == "US";

    /// <summary>Dice si el SWIFT/BIC tiene ocho u once letras o números.</summary>
    public static bool IsSwiftBic(string? value) => Compact(value) is { } swift && SwiftBicPattern().IsMatch(swift);

    /// <summary>Dice si el ABA tiene nueve dígitos.</summary>
    public static bool IsAba(string? value) => Compact(value) is { } aba && AbaPattern().IsMatch(aba);

    /// <summary>Dice si el número está en blanco o, sin espacios, cabe en <see cref="AccountNumberMaxLength"/>.</summary>
    public static bool FitsAnAccountNumber(string? value) => (Compact(value)?.Length ?? 0) <= AccountNumberMaxLength;

    /// <summary>
    /// Un número con forma de IBAN tiene que cuadrar su dígito de control; uno sin esa forma es un
    /// número de cuenta local, que no lleva dígito que revisar.
    /// </summary>
    public static bool HasValidIbanChecksumWhenIban(string? value)
    {
        var number = Compact(value);
        return number is null || !IbanPattern().IsMatch(number) || HasValidIbanChecksum(number);
    }

    /// <summary>
    /// ISO 13616: las cuatro primeras posiciones van al final, cada letra pasa a dos dígitos
    /// (A = 10 ... Z = 35) y el número resultante módulo 97 debe dar 1.
    /// </summary>
    private static bool HasValidIbanChecksum(string iban)
    {
        var remainder = 0;
        foreach (var c in iban[4..] + iban[..4])
        {
            var value = char.IsDigit(c) ? c - '0' : c - 'A' + 10;
            remainder = (value > 9 ? remainder * 100 : remainder * 10) + value;
            remainder %= 97;
        }
        return remainder == 1;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex CountryPattern();

    [GeneratedRegex("^[A-Z0-9]{8}([A-Z0-9]{3})?$")]
    private static partial Regex SwiftBicPattern();

    [GeneratedRegex(@"^\d{9}$")]
    private static partial Regex AbaPattern();

    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$")]
    private static partial Regex IbanPattern();
}
