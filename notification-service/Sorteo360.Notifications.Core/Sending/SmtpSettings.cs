namespace Sorteo360.Notifications.Core.Sending;

/// <summary>
/// Por dónde sale el correo. La sección se llama <c>Smtp</c> y no <c>SendGrid</c> a propósito: si
/// IT no abre el puerto saliente el plan B es la Web API sobre 443, y un nombre de proveedor
/// habría que renombrarlo justo el día en que ya se está reescribiendo el transporte.
/// <para>
/// Mailpit es un servidor SMTP de verdad, así que el camino que corre contra él es el mismo que
/// corre contra el proveedor. La única línea que difiere es <see cref="RequireTls"/>.
/// </para>
/// </summary>
public sealed class SmtpSettings
{
    public static string SectionName => "Smtp";

    /// <summary>Host del servidor. <c>mailpit</c> en local y en stage.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>Puerto. 1025 en Mailpit, 587 en el proveedor.</summary>
    public int Port { get; set; }

    /// <summary>
    /// Si la sesión exige STARTTLS. En <c>true</c> falla cerrado, que es lo que se quiere en
    /// producción; en <c>false</c> la sesión va en texto plano, que es lo que sirve Mailpit.
    /// Nunca hay un tercer valor: negociar "si está disponible" es no saber qué corrió.
    /// </summary>
    public bool RequireTls { get; set; } = true;

    /// <summary>Usuario. En el proveedor es la palabra literal <c>apikey</c>.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Contraseña. En el proveedor es la API key.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Si este entorno tiene permiso para mandar correo de verdad hacia afuera. Es lo que mira el
    /// freno de arranque, junto con la contraseña.
    /// </summary>
    public bool AllowExternalRelay { get; set; }

    /// <summary>Si la sesión lleva credenciales. Sin contraseña no hay AUTH que intentar.</summary>
    public bool HasCredentials => !string.IsNullOrWhiteSpace(Password);
}
