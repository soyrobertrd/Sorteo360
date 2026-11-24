using System.Security.Cryptography;

namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Genera números de boleto aleatorios de seis dígitos con guion (<c>482-917</c>), únicos dentro
/// de cada sorteo y generados criptográficamente para que no revelen orden de registro.
/// </summary>
public static class TicketNumberGenerator
{
    public const int ExclusiveUpperBound = 1_000_000;
    public const int MaxAttempts = 100;

    /// <summary>Formatea un entero entre 0 y 999,999 como <c>000-000</c>.</summary>
    public static string Format(int number)
    {
        if (number is < 0 or >= ExclusiveUpperBound)
            throw new ArgumentOutOfRangeException(nameof(number), number, "El número de boleto debe estar entre 0 y 999,999.");

        return $"{number / 1000:D3}-{number % 1000:D3}";
    }

    /// <summary>
    /// Devuelve un número de boleto que no esté en <paramref name="used"/> usando entropía
    /// criptográfica por defecto (o <paramref name="nextInt"/> en pruebas unitarias).
    /// </summary>
    public static string NextUnique(IReadOnlySet<string> used, Func<int>? nextInt = null)
    {
        ArgumentNullException.ThrowIfNull(used);

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var value = nextInt is null
                ? RandomNumberGenerator.GetInt32(0, ExclusiveUpperBound)
                : nextInt();
            var candidate = Format(value);
            if (!used.Contains(candidate))
                return candidate;
        }

        throw new InvalidOperationException("No quedan números de boleto libres para este sorteo.");
    }
}

