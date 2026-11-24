namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// Por dónde sale el correo de una campaña: el valor de <c>Notifications__Transport</c>, ya
/// validado (spec <c>04</c> §4).
/// <para>
/// <b>Es un valor y no una rama de entorno</b> a propósito. <c>docker-compose.dokploy.yml</c> es
/// un solo archivo autocontenido que despliega stage y producción, así que los dos corren el
/// mismo YAML y difieren solo en lo que se les inyecta. Un <c>IsProduction</c> sería una
/// divergencia escondida incluso de ese archivo, y lo que se quiere es que stage se comporte como
/// se va a comportar producción. Es también lo que deja a stage pasarse a <c>http</c> el día que
/// notification-service exista, cambiando una variable y nada más (§4.2).
/// </para>
/// </summary>
public static class CampaignSendTransport
{
    /// <summary>El stub con su tabla: registra el correo de cada persona y no sale nada.</summary>
    public const string Stub = "stub";

    /// <summary>El cliente tipado real contra notification-service.</summary>
    public const string Http = "http";

    /// <summary>La clave de configuración, para que el nombre no se escriba dos veces.</summary>
    public const string SettingKey = "Notifications:Transport";

    /// <summary>
    /// El transporte que declara la configuración, en minúsculas.
    /// <para>
    /// <b>Valida el vocabulario del contrato, no qué está construido.</b> Los dos valores son
    /// legítimos en la spec <c>04</c> §4, así que los dos pasan por acá; que hoy solo uno tenga
    /// implementación es cosa del registro de dependencias, que es quien la elige y quien se cae
    /// si le piden la que falta. Meter esa segunda cosa acá dejaría a Core opinando sobre lo que
    /// hay en Infrastructure.
    /// </para>
    /// <para>
    /// <b>Sin valor por omisión, a propósito.</b> Que falte tiene que tumbar el arranque, no
    /// elegir algo: enlazar el stub cuando la variable no llega —el olvido exacto del que avisa
    /// el <c>CLAUDE.md</c> de la raíz, una clave que llega al compose base y nunca al de
    /// despliegue— haría que las campañas dijeran que salieron sin escribirle a nadie, en
    /// silencio, en la función estrella del cliente.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// El valor falta, está vacío o no es ninguno de los dos.
    /// </exception>
    public static string Require(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                $"Falta '{SettingKey}' ({Stub} o {Http}). No tiene valor por omisión: sin él el "
                + "servicio aceptaría campañas y no le escribiría a nadie.");
        }

        var transport = configured.Trim().ToLowerInvariant();

        if (transport is not (Stub or Http))
        {
            throw new InvalidOperationException(
                $"'{SettingKey}' vale '{configured}', que no es un transporte: se admiten '{Stub}' y '{Http}'.");
        }

        return transport;
    }
}
