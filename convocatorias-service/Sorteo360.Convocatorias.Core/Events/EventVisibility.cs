namespace Sorteo360.Convocatorias.Core.Events;

/// <summary>
/// Si el evento se publica o no, tal como lo declara events-service. Igual que
/// <see cref="EventStatus"/>, es una copia de un conjunto ajeno: la decisión vive allá.
/// </summary>
public static class EventVisibility
{
    public const string Public  = "public";
    public const string Private = "private";
}
