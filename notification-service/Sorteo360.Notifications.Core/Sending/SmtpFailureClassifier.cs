namespace Sorteo360.Notifications.Core.Sending;

/// <summary>Qué hacer con una transacción SMTP fallida.</summary>
public enum SmtpFailureKind
{
    /// <summary>Volver a intentarlo más tarde.</summary>
    Transient,

    /// <summary>No volver a intentarlo nunca.</summary>
    Terminal,

    /// <summary>
    /// No volver a intentarlo, y decirlo fuerte: el fallo es nuestra credencial, no el buzón de
    /// quien recibe. Ningún reintento lo arregla y nadie se entera si no se registra como error.
    /// </summary>
    AuthenticationFailure
}

/// <summary>
/// La regla de reintento, que es de SMTP y no nuestra: <c>4xx</c> quiere decir inténtalo más
/// tarde, <c>5xx</c> quiere decir nunca. No hay nada que inventar acá, solo que aplicarlo.
/// </summary>
public static class SmtpFailureClassifier
{
    /// <summary>Fallo de autenticación. Es terminal y además es culpa nuestra.</summary>
    public const int AuthenticationFailed = 535;

    /// <summary>
    /// Clasifica el código con el que murió la transacción. Sin código —conexión rechazada, tiempo
    /// agotado, handshake TLS fallido— es transitorio: no llegó a haber conversación, así que el
    /// servidor no dijo que no.
    /// </summary>
    public static SmtpFailureKind Classify(int? statusCode) => statusCode switch
    {
        null => SmtpFailureKind.Transient,
        AuthenticationFailed => SmtpFailureKind.AuthenticationFailure,
        >= 400 and < 500 => SmtpFailureKind.Transient,
        _ => SmtpFailureKind.Terminal
    };
}
