using System.Globalization;
using System.Text;
using CSharpFunctionalExtensions;

namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// La forma en que se comparan dos empresas para la regla de un premio por empresa (spec 02 §1.1).
/// La empresa es texto libre que escribió cada uno, así que dos son la misma cuando coinciden sin
/// espacios de más, sin tildes y sin distinguir mayúsculas. Nada más: ni sufijos como "SRL" ni
/// parecidos. Una exclusión que no se le puede explicar a un notario es peor que una que se escapa.
/// <para>
/// Cultura invariante, para que el resultado no dependa de dónde corre el servidor.
/// </para>
/// </summary>
public static class CompanyKey
{
    /// <summary>
    /// La clave de una empresa, o <see cref="Maybe{T}.None"/> si viene vacía. Una empresa en blanco
    /// nunca choca con nada: si no, todos los que dejaron el campo vacío serían una sola empresa.
    /// </summary>
    public static Maybe<string> Normalize(string? company)
    {
        if (string.IsNullOrWhiteSpace(company))
            return Maybe<string>.None;

        var collapsed = string.Join(' ', company.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return StripDiacritics(collapsed).ToUpperInvariant();
    }

    private static string StripDiacritics(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
