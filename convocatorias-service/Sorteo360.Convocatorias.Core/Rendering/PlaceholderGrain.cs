namespace Sorteo360.Convocatorias.Core.Rendering;

/// <summary>
/// De qué depende el valor de un marcador. El grano decide qué pasa cuando falta, y son dos
/// políticas distintas a propósito (ver <see cref="SupportedPlaceholder"/>).
/// </summary>
public enum PlaceholderGrain
{
    /// <summary>
    /// Su valor sale del evento y es el mismo para todo el mundo. Si falta, se rechaza el
    /// lanzamiento nombrando el campo: nadie manda "se celebrará en ." a mil personas.
    /// </summary>
    Event,

    /// <summary>
    /// Su valor sale de cada persona. Si falta, se cae a un valor neutro: bloquear una campaña
    /// de mil por una fila importada sin nombre es la cola moviendo al perro.
    /// </summary>
    Recipient,

    /// <summary>
    /// Su valor lo escribió quien armó la campaña, en la propia fila: el asunto y el texto de un
    /// mensaje directo. Es el mismo para todo el mundo y no puede faltar, porque crear y editar
    /// ya rechazan un mensaje directo sin ellos.
    /// </summary>
    Campaign
}
