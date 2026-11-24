namespace Sorteo360.Convocatorias.Core.Templates;

/// <summary>
/// Las claves del conjunto del día uno. Son <b>provisionales</b>: MITUR ya manda estos correos
/// en el sistema que esta plataforma reemplaza y su dueño está recuperando la copia real, en
/// <c>es</c>, <c>en</c> y <c>fr</c>. El modelo no depende de la lista; la lista sí puede cambiar.
/// </summary>
public static class TemplateKey
{
    /// <summary>Reserva la fecha. Campaña.</summary>
    public const string SaveTheDate = "save_the_date";

    /// <summary>Invitación a registrarse. Campaña.</summary>
    public const string RegistrationInvite = "registration_invite";

    /// <summary>Confirmación del registro que la persona acaba de hacer. Transaccional.</summary>
    public const string RegistrationConfirmation = "registration_confirmation";

    /// <summary>Aviso de que el evento se canceló, a quien se había registrado. Transaccional.</summary>
    public const string EventCancelled = "event_cancelled";

    /// <summary>
    /// Un mensaje directo, sin evento detrás: el asunto y el texto los escribe quien arma la
    /// campaña. Campaña, y la única que no convoca a nada (PGTI-174).
    /// </summary>
    public const string DirectMessage = "direct_message";
}
