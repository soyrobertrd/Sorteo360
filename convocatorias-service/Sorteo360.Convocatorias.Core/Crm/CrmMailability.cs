namespace Sorteo360.Convocatorias.Core.Crm;

/// <summary>
/// Lo que el CRM contesta sobre una persona cuando se le vuelve a preguntar si se le puede
/// escribir, justo antes de lanzar. Tres campos y ninguno más: la recomprobación no necesita la
/// ficha, necesita el veredicto (spec <c>04</c> §3).
/// <para>
/// <b>Un id que no vuelve no aparece acá</b>, y esa ausencia es el dato: el CRM no puede
/// informar sobre una fila que no devuelve, así que quien llama la convierte en
/// <see cref="Campaigns.ExclusionReason.ContactUnavailable"/>. El caso real es un contacto
/// borrado en blando entre la selección y el lanzamiento.
/// </para>
/// </summary>
/// <param name="Id">Identidad del contacto en el CRM.</param>
/// <param name="CanBeEmailed">La respuesta del CRM, calculada por él y nunca por nosotros.</param>
/// <param name="SuppressionReason">Su código de motivo, nulo cuando sí se le puede escribir.</param>
public sealed record CrmMailability(Guid Id, bool CanBeEmailed, string? SuppressionReason);
