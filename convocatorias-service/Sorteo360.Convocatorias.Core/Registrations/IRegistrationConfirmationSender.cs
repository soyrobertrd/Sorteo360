using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Events;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Registrations;

/// <summary>
/// Manda el correo de confirmación de una inscripción (spec <c>06</c> §4.4): la plantilla
/// <c>registration_confirmation</c> en el idioma del evento, con el nombre, la referencia, la URL
/// del QR y el enlace a la entrada. Un recipiente, nada rastreado por persona, así que sale por el
/// transporte transaccional y no como una campaña.
/// <para>
/// <b>Su fallo no tumba la inscripción.</b> Devuelve un <see cref="Result"/> para que quien llama
/// lo registre, pero la inscripción ya está guardada y la respuesta es la misma con correo o sin
/// él: si el transporte no contesta, la persona tiene el QR en pantalla y la referencia impresa
/// (spec <c>06</c> §4.4). Por eso el endpoint lo llama y sigue.
/// </para>
/// </summary>
public interface IRegistrationConfirmationSender
{
    /// <summary>
    /// Manda la confirmación de <paramref name="registration"/> para <paramref name="summary"/>.
    /// </summary>
    /// <param name="registration">La inscripción recién creada, o la que ya había en un reenvío.</param>
    /// <param name="summary">El evento, del que salen el idioma y los valores del correo.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<UnitResult<BaseError>> SendAsync(
        Registration registration,
        EventSummary summary,
        CancellationToken cancellationToken);
}
