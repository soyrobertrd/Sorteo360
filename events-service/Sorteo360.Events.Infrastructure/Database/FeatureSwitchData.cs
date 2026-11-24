namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Entidad de persistencia para los interruptores de funciones (<c>events.feature_switch</c>).
/// Solo se almacenan claves configuradas explícitamente; cualquier clave ausente se considera encendida.
/// </summary>
internal sealed class FeatureSwitchData
{
    public string Key { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public Guid? ChangedById { get; set; }
    public string? ChangedByName { get; set; }
    public DateTimeOffset? ChangedAt { get; set; }
}

