namespace Sorteo360.Convocatorias.Core.Shared;

/// <summary>
/// Si algo tiene la <b>forma</b> de una dirección de correo. No si existe: eso lo sabe el
/// servidor del otro lado y nadie más.
/// <para>
/// <b>Deliberadamente laxa</b>, y por dos motivos distintos que casualmente piden lo mismo. Al
/// leer una hoja de cálculo lo que caza es la columna equivocada, no la errata (spec <c>05</c>
/// §4). Al entregar un lote es el único rechazo que existe en el momento de aceptar, que es
/// justo lo que mantiene <c>failed</c> casi vacío (spec <c>04</c> §2.1).
/// </para>
/// <para>
/// Vive acá, y no en cada uno de esos dos sitios, porque dos copias de "qué parece una
/// dirección" es como se separan: la del envío se volvería más estricta que la de la carga y una
/// fila aceptada al subirla acabaría rechazada al mandarla.
/// </para>
/// </summary>
public static class EmailAddressShape
{
    /// <summary>
    /// Una arroba, algo a cada lado, un punto en el dominio y ningún espacio.
    /// <para>
    /// Recorta los bordes ella misma. Si esperara el valor ya limpio, los dos sitios que la usan
    /// podrían pasarle cosas distintas —uno recorta y el otro no— y una dirección aceptada al
    /// subirla acabaría rechazada al mandarla, que es la asimetría que compartir la regla existe
    /// para evitar.
    /// </para>
    /// </summary>
    public static bool IsWellFormed(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        value = value.Trim();

        if (value.Any(char.IsWhiteSpace))
            return false;

        var at = value.IndexOf('@', StringComparison.Ordinal);

        if (at <= 0 || at != value.LastIndexOf('@') || at == value.Length - 1)
            return false;

        var domain = value[(at + 1)..];

        return domain.Contains('.', StringComparison.Ordinal)
            && !domain.StartsWith('.')
            && !domain.EndsWith('.');
    }

    /// <summary>
    /// La clave por la que dos direcciones cuentan como la misma: <b>recorte y minúsculas y nada
    /// más</b>. Sin quitar puntos de gmail y sin tratar el <c>+etiqueta</c>, ni acá ni en ninguna
    /// otra parte: eso es una decisión sobre quién cuenta como una sola persona, y pertenece a la
    /// regla de unicidad del CRM o a ningún sitio (spec <c>05</c> §5).
    /// <para>
    /// Vive al lado de <see cref="IsWellFormed"/> y por el mismo motivo. La usan el deduplicado
    /// de una carga y la unicidad de una inscripción (spec <c>06</c> §4.1), que es una restricción
    /// de la base: dos copias que derivaran una de otra dejarían filas que la columna ya no puede
    /// distinguir, y eso no se arregla con una migración.
    /// </para>
    /// </summary>
    public static string Normalize(string email)
    {
        ArgumentNullException.ThrowIfNull(email);

        return email.Trim().ToLowerInvariant();
    }
}
