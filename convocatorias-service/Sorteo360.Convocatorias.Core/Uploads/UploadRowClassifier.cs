using Sorteo360.Convocatorias.Core.Shared;

namespace Sorteo360.Convocatorias.Core.Uploads;

/// <summary>
/// Las dos primeras reglas de §4, las que se deciden mirando solo el archivo: mal formada, y
/// duplicada dentro del archivo. Sin E/S y sin estado, para que sea lo que se puede probar
/// entero con una lista en memoria.
/// <para>
/// Lo que queda sin cubo sale con <see cref="ClassifiedUploadRow.Bucket"/> nulo, porque los
/// cuatro cubos restantes necesitan preguntarle al CRM.
/// </para>
/// </summary>
public static class UploadRowClassifier
{
    /// <summary>
    /// Clasifica las filas conservando el orden del archivo, y devuelve además cuántas filas
    /// idénticas se plegaron en otra.
    /// </summary>
    public static ClassifiedUpload Classify(IReadOnlyList<ParsedUploadRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var classified = new List<ClassifiedUploadRow>(rows.Count);
        var candidates = new List<(ParsedUploadRow Row, string Key)>();

        foreach (var row in rows)
        {
            var reason = MalformedReason(row);

            if (reason is not null)
            {
                classified.Add(new ClassifiedUploadRow(row, UploadRowBucket.Malformed, reason, null, [], 0));
                continue;
            }

            // La clave local del deduplicado sale de `EmailAddressShape` y no de una copia suya:
            // la misma regla decide la unicidad de una inscripción, que es una restricción de la
            // base (spec `06` §4.1), y dos reglas que se separaran dejarían filas que esa columna
            // ya no puede distinguir.
            candidates.Add((row, EmailAddressShape.Normalize(row.Email!)));
        }

        var collapsed = 0;

        // El grano de la deduplicación es el correo normalizado. Se agrupa después de descartar
        // las mal formadas: una fila sin correo no puede duplicar a nadie.
        foreach (var group in candidates.GroupBy(candidate => candidate.Key, StringComparer.Ordinal))
        {
            var members = group.Select(candidate => candidate.Row).ToList();

            if (members.Count == 1)
            {
                classified.Add(new ClassifiedUploadRow(members[0], null, null, group.Key, [], 0));
                continue;
            }

            if (AreIdentical(members))
            {
                // Copiar y pegar la misma fila tres veces es el caso común, y ahí plegar es
                // obviamente lo correcto: nadie tiene que elegir entre dos filas iguales.
                collapsed += members.Count - 1;
                classified.Add(new ClassifiedUploadRow(members[0], null, null, group.Key, [], members.Count - 1));
                continue;
            }

            // Comparten correo y no dicen lo mismo. Van <b>todas</b> al cubo y no se crea
            // ninguna: el archivo se contradice a sí mismo y no tenemos con qué desempatar.
            // Quedarse con la primera elegiría un ganador sin pruebas y sin que nadie se entere
            // de que hubo desacuerdo; quedarse con la última haría además que el desenlace
            // dependiera del orden de las filas.
            var numbers = members.Select(member => member.SourceRowNumber).Order().ToList();

            classified.AddRange(members.Select(member => new ClassifiedUploadRow(
                member,
                UploadRowBucket.DuplicateInFile,
                null,
                group.Key,
                numbers,
                0)));
        }

        return new ClassifiedUpload(
            [.. classified.OrderBy(row => row.Row.SourceRowNumber)],
            collapsed);
    }

    /// <summary>
    /// El motivo por el que la fila no sirve, o <c>null</c> si sirve. El orden es el de §4 y gana
    /// el primero.
    /// </summary>
    private static string? MalformedReason(ParsedUploadRow row)
    {
        if (string.IsNullOrWhiteSpace(row.Email))
            return MalformedRowReason.MissingEmail;

        if (!EmailAddressShape.IsWellFormed(row.Email))
            return MalformedRowReason.InvalidEmail;

        // Los dos, no uno: el CRM exige nombre y apellido al crear un contacto, y dar por bueno
        // medio nombre obligaría a inventar la otra mitad al confirmar.
        if (string.IsNullOrWhiteSpace(row.FirstName) || string.IsNullOrWhiteSpace(row.LastName))
            return MalformedRowReason.MissingName;

        return null;
    }

    /// <summary>
    /// Si todas las filas del grupo dicen lo mismo en las cinco columnas, ya recortadas. El
    /// correo se compara sin distinguir mayúsculas porque es la clave con la que se agruparon:
    /// exigir la misma grafía mandaría a <c>duplicate_in_file</c> dos filas que solo difieren en
    /// una mayúscula del dominio, que es precisamente el pegado que plegar resuelve bien.
    /// </summary>
    private static bool AreIdentical(IReadOnlyList<ParsedUploadRow> rows)
    {
        var first = rows[0];

        return rows.Skip(1).All(row =>
            Same(row.FirstName, first.FirstName)
            && Same(row.LastName, first.LastName)
            && Same(row.Phone, first.Phone)
            && Same(row.Company, first.Company)
            && string.Equals(row.Email?.Trim(), first.Email?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Vacío y ausente son lo mismo a la hora de comparar dos filas.</summary>
    private static bool Same(string? left, string? right) =>
        string.Equals(left?.Trim() ?? string.Empty, right?.Trim() ?? string.Empty, StringComparison.Ordinal);
}

/// <summary>
/// El archivo ya clasificado por lo que se puede decidir sin salir de él.
/// </summary>
/// <param name="Rows">Las filas, en el orden del archivo, menos las que se plegaron.</param>
/// <param name="CollapsedDuplicates">
/// Cuántas filas idénticas desaparecieron plegadas en otra. Es el séptimo sumando de la
/// ecuación de §4, y sin él las cuentas no cuadran contra el número de filas del archivo.
/// </param>
public sealed record ClassifiedUpload(IReadOnlyList<ClassifiedUploadRow> Rows, int CollapsedDuplicates);
