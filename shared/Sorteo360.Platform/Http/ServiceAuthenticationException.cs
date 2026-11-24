namespace Sorteo360.Platform.Http;

/// <summary>
/// El servicio no pudo obtener su propio token para llamar a otro servicio: el emisor no
/// responde, rechaza las credenciales o devuelve algo que no es un token.
/// <para>
/// Deriva de <see cref="HttpRequestException"/> a propósito: para quien hace la llamada
/// saliente es un fallo de la dependencia como cualquier otro, así que el <c>catch</c> que
/// ya traduce a <see cref="Errors.ErrorKind.Unavailable"/> la recoge sin conocer este tipo.
/// </para>
/// </summary>
public sealed class ServiceAuthenticationException : HttpRequestException
{
    public ServiceAuthenticationException(string message)
        : base(message)
    {
    }

    public ServiceAuthenticationException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
