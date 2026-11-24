namespace Sorteo360.Platform.Files;

/// <summary>
/// Por qué <see cref="FileContentInspector"/> no admite un archivo. Es un motivo y no un error:
/// el código y el mensaje que ve el cliente los pone cada servicio, que es quien sabe a quién le
/// habla.
/// </summary>
public enum FileRejection
{
    /// <summary>Pesa más del límite. El tamaño se cuenta, no se pregunta.</summary>
    TooLarge,

    /// <summary>Los bytes no son de ninguna de las clases que se guardan.</summary>
    TypeNotAllowed,

    /// <summary>
    /// Una foto HEIC. Va aparte del genérico porque es el rechazo que más gente encuentra, y
    /// merece un mensaje que diga qué cambiar.
    /// </summary>
    HeicNotSupported,

    /// <summary>Los bytes son de una clase admitida, pero el tipo declarado o la extensión dicen otra.</summary>
    TypeMismatch
}
