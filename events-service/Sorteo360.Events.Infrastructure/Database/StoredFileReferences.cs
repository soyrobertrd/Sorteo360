namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Las tablas que enlazan con <c>events.stored_file</c>, en un solo lugar: las tablas de enlace y
/// también las que lo apuntan desde una columna propia, como el logo del patrocinador en <c>prize</c>.
/// <para>
/// Existe por la purga de retención que todavía no está escrita: "¿este archivo sigue
/// referenciado?" es, si no, una consulta que alguien olvida extender cuando aparece una tabla de
/// enlace nueva, y la purga borra archivos vivos. Un test compara esta lista contra el modelo de
/// EF, así que agregar una tabla que apunte a <c>stored_file</c> sin registrarla acá deja la
/// suite en rojo.
/// </para>
/// </summary>
internal static class StoredFileReferences
{
    /// <summary>Nombre de cada tabla del esquema <c>events</c> con una FK hacia <c>stored_file</c>.</summary>
    public static readonly IReadOnlyList<string> LinkTables =
    [
        "event_image",
        "prize_handover",
        "notarial_record",
        "prize"
    ];
}
