namespace Sorteo360.Events.Web.FileEndpoints;

/// <summary>
/// Un archivo que el almacenamiento dejó de tener, visto por lo que hace falta para ir a
/// buscarlo: cómo se llamaba, quién lo subió, cuándo, y desde cuándo falta.
/// <para>
/// Lleva el autor de la carga, a diferencia de <see cref="FileResponse"/>: en una pantalla el
/// autor no aporta nada, pero para restaurar el archivo es a quien hay que preguntarle qué era.
/// La clave de almacenamiento sigue sin salir — quien restaura lo hace por nombre y fecha desde
/// la papelera del almacenamiento, no por una ruta que este servicio no controla.
/// </para>
/// </summary>
public sealed record MissingFileResponse
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public Guid CreatedById { get; init; }

    /// <summary>Desde cuándo se sabe que falta: la primera lectura que se topó con el hueco.</summary>
    public DateTimeOffset MissingSince { get; init; }
}
