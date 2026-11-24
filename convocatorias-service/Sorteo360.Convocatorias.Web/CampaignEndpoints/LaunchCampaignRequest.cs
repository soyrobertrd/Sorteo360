namespace Sorteo360.Convocatorias.Web.CampaignEndpoints;

/// <summary>
/// Lanzar. Un solo campo, y <b>obligatorio</b>: omitirlo es un <c>400</c> y no un lanzamiento
/// que se salta la comprobación.
/// <para>
/// Un campo opcional acá fallaría abierto justo en el cliente que más necesita la guarda: uno
/// escrito deprisa contra Scalar, que es como pasa cada lanzamiento hasta que Roberto construya
/// la pantalla. Se declara anulable para poder distinguir "no vino" de "vino un cero", que es
/// lo que el validador rechaza.
/// </para>
/// </summary>
public sealed record LaunchCampaignRequest
{
    /// <summary>Los destinatarios que quien lanza tenía en pantalla.</summary>
    public int? ExpectedRecipientCount { get; init; }
}
