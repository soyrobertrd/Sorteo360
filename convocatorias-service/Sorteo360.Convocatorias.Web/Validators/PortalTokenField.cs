namespace Sorteo360.Convocatorias.Web.Validators;

/// <summary>
/// El tope de longitud de un token del portal, compartido por todas las rutas públicas que reciben uno.
/// <para>
/// <b>Holgado a propósito.</b> Un token mide 43 caracteres (spec <c>06</c> §2.1), pero ajustar
/// el tope a esa medida convertiría la longitud en una respuesta: quien prueba tokens sabría por
/// el <c>400</c> que la suya era la correcta. Uno inválido tiene que salir por el mismo rechazo
/// genérico que todos los demás.
/// </para>
/// <para>
/// Escrito una vez y no una por validador: dos topes que se separaran dejarían una ruta
/// aceptando lo que la otra rechaza sobre la misma credencial.
/// </para>
/// </summary>
internal static class PortalTokenField
{
    public const int MaximumLength = 200;
}
