namespace Sorteo360.Events.Core.Events;

/// <summary>
/// Qué papel cumple una imagen dentro del evento. Los bytes no lo pueden decir: la portada y una
/// foto de la galería son las dos una imagen, y quien arma la página pública no las pone en el
/// mismo sitio.
/// <para>
/// El conjunto es cerrado y se persiste como texto. <see cref="Gallery"/> es el primer valor a
/// propósito, y es el que rellena una fila que no diga cuál es: la portada es única por evento,
/// así que caer en ella por omisión reemplazaría la que hubiera sin que nadie lo pidiera.
/// </para>
/// </summary>
public enum EventImageKind
{
    /// <summary>Una foto más del evento, para la galería promocional.</summary>
    Gallery,

    /// <summary>La imagen que encabeza el evento. Hay una sola por evento.</summary>
    Cover
}
