namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// Los dos hashes que le quedan a una fila después de que su correo saliera. El texto plano se fue
/// dentro del enlace y no se guarda en ninguna parte (spec <c>06</c> §2.1).
/// <para>
/// <b>Se escriben en la misma transacción que anota el desenlace</b>, y solo para las filas que
/// llegan a <c>handed_off</c>. Escribirlos antes de llamar al transporte dejaría un lote rechazado
/// con sus mil filas pendientes y con hashes cuyo texto plano ya no existe: como acuñarles un
/// segundo token está prohibido —rompería el enlace del correo que sí salió—, esas mil personas no
/// se podrían volver a entregar nunca. Un lote rechazado es el caso normal de una reanudación, no
/// uno raro.
/// </para>
/// <para>
/// Lo que sí se acepta con este orden: entre el "aceptado" del transporte y esta escritura hay una
/// ventana. Si la escritura falla ahí, el correo salió con un enlace cuyo hash no se guardó y ese
/// enlace queda muerto; las mismas filas vuelven a salir pendientes y sin hash, reciben un segundo
/// correo, y ese sí funciona.
/// </para>
/// <para>
/// El de registro es nulo en un mensaje directo: no hay evento al que inscribirse, así que no se
/// acuña, y resolver un enlace de registro que no existe contesta el <c>404</c> de siempre.
/// </para>
/// </summary>
public sealed record RecipientTokenHashes(
    Guid RecipientId,
    byte[]? RegistrationTokenHash,
    byte[] UnsubscribeTokenHash);
