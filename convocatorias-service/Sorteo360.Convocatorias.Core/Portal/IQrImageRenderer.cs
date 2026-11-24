namespace Sorteo360.Convocatorias.Core.Portal;

/// <summary>
/// Convierte una URL en la imagen PNG de su código QR (spec <c>06</c> §4.5).
/// <para>
/// <b>Vive detrás de una interfaz aunque hoy solo la implemente una librería</b>: la generación
/// usa un paquete que solo conoce Infrastructure, y el dominio no lo referencia. Lo que le pide
/// el portal es "esta URL, hecha imagen", y con eso basta para probar el endpoint contra un doble.
/// </para>
/// <para>
/// <b>Se genera al vuelo y no se guarda</b> (spec <c>06</c> §4.5): la imagen no se escribe en
/// disco ni pide un volumen. Son bytes que nacen y mueren en la petición.
/// </para>
/// </summary>
public interface IQrImageRenderer
{
    /// <summary>
    /// El PNG del código QR que codifica <paramref name="payload"/>.
    /// </summary>
    /// <param name="payload">La URL que se codifica. Para la entrada, la página del portal.</param>
    /// <returns>Los bytes del PNG, listos para servir con <c>image/png</c>.</returns>
    byte[] RenderPng(string payload);
}
