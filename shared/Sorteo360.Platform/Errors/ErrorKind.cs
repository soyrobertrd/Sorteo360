namespace Sorteo360.Platform.Errors;

/// <summary>
/// Clasifica la naturaleza de un <see cref="BaseError"/> de forma agnóstica al transporte.
/// La capa Web traduce este tipo al estado HTTP correspondiente; el dominio nunca conoce
/// códigos HTTP.
/// <para>
/// Quien construye un <see cref="BaseError"/> sin indicar la clase obtiene
/// <see cref="Internal"/>, porque así lo declara el parámetro por omisión del constructor.
/// No es el valor cero del enum, que es <see cref="NotFound"/>: nunca hay que confiar en
/// <c>default(ErrorKind)</c> para obtener un fallo no clasificado.
/// </para>
/// </summary>
public enum ErrorKind
{
    /// <summary>El recurso solicitado no existe (se traduce a 404).</summary>
    NotFound,

    /// <summary>La operación no es válida para el estado actual del recurso (se traduce a 409).</summary>
    Conflict,

    /// <summary>
    /// El usuario actual, en concreto, no puede realizar esta operación aunque su rol se
    /// la permita en general (se traduce a 403). Es una regla de negocio sobre la identidad
    /// del actor, no un problema de estado del recurso.
    /// </summary>
    Forbidden,

    /// <summary>Fallo de infraestructura o no clasificado (se traduce a 500).</summary>
    Internal,


    /// <summary>
    /// Una dependencia externa no responde: otro servicio de la plataforma, el emisor de
    /// tokens, la red entre ellos (se traduce a 503). Se distingue de <see cref="Internal"/>
    /// a propósito: este servicio está sano, quien falla es aquello de lo que depende, y un
    /// 500 le atribuiría la avería.
    /// </summary>
    Unavailable,

    /// <summary>
    /// La petición está bien formada pero su contenido incumple una regla que el validador del
    /// borde no podía comprobar (se traduce a 400). Se distingue de <see cref="Conflict"/> a
    /// propósito: no hay nada en el estado del recurso que impida la operación, lo que no sirve
    /// es lo que llegó.
    /// </summary>
    Invalid,

    /// <summary>
    /// Quien llama no demostró quién es: unas credenciales que el emisor de tokens rechazó (se
    /// traduce a 401). Se distingue de <see cref="Forbidden"/> a propósito: ahí la identidad se
    /// conoce y lo que falta es permiso, aquí no hay identidad que valga.
    /// </summary>
    Unauthenticated,

    /// <summary>
    /// Quien llama superó el número de intentos permitido en la ventana (se traduce a 429). No
    /// dice nada de la petición en sí: la misma, un minuto después, puede salir bien.
    /// </summary>
    TooManyRequests
}
