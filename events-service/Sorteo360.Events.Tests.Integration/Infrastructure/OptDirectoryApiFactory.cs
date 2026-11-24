namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>
/// La API con la copia del directorio siempre vencida, para los tests que van sobre el directorio
/// mismo.
/// <para>
/// Con TTL cero cada lectura intenta renovar, que es justo lo que hay que ejercitar: así se ve si
/// una renovación fallida sirve la copia anterior, y ningún test hereda la copia que dejó otro.
/// </para>
/// </summary>
public sealed class OptDirectoryApiFactory : EventsApiDbFactory
{
    /// <inheritdoc />
    protected override int OptDirectoryTtlMinutes => 0;
}
