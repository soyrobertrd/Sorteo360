namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// La lista de DO Travel Awards en vigor: una sola para toda la plataforma.
/// <para>
/// <paramref name="Members"/> son correos normalizados, comparados tal cual contra el de cada
/// asistente. <paramref name="BatchId"/> viaja con los miembros que describe para que al arrancar
/// el sorteo quede escrito qué lista se usó de verdad.
/// </para>
/// </summary>
public sealed record AwardsList(Guid BatchId, IReadOnlySet<string> Members);
