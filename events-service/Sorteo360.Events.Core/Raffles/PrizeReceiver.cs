namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Quién retiró el premio, tal como lo declara el operador (spec 03 §3.1). Que sea el ganador lo
/// dice el operador y nunca se deduce del nombre; el documento se pide igual cuando lo es.
/// </summary>
public sealed record PrizeReceiver(string Name, string IdentityDocument, bool IsWinner);
