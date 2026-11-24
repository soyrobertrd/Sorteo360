using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// La campaña no cumple la regla del mensaje directo: no tiene evento <b>si y solo si</b> sale con
/// la plantilla <c>direct_message</c>, y entonces lleva su asunto y su texto; una campaña de evento
/// no lleva ninguno de los dos. Lo rechazan crear y editar, antes de escribir nada (PGTI-174).
/// <para>
/// Un solo código para las cuatro formas de incumplirla, porque las cuatro son el mismo error de
/// quien llama: la pantalla del mensaje directo y la de la campaña de evento son dos pantallas
/// distintas, y ninguna de las dos tiene un camino legítimo hasta una mezcla.
/// </para>
/// </summary>
public sealed record CampaignDirectMessageMismatchError(string Detail)
    : BaseError(
        $"Una campaña sin evento es un mensaje directo, con su asunto y su texto, y una campaña de evento no lleva ninguno de los dos: {Detail}",
        ErrorCode,
        ErrorKind.Invalid)
{
    public const string ErrorCode = "CAMPAIGN_DIRECT_MESSAGE_MISMATCH";
}
