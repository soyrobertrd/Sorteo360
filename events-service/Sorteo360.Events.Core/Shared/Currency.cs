namespace Sorteo360.Events.Core.Shared;

/// <summary>
/// Monedas soportadas (conjunto cerrado ISO 4217). La OPT solo opera en USD y
/// EUR y no se calcula tipo de cambio. Se persiste como texto y el contrato
/// HTTP también usa texto; el dominio usa este enum.
/// </summary>
public enum Currency
{
    USD,
    EUR
}
