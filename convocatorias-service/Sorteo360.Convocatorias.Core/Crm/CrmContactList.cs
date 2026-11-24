namespace Sorteo360.Convocatorias.Core.Crm;

/// <summary>
/// Una lista de contactos del CRM: un conjunto fijo que alguien de una OPT armó a mano. El CRM
/// es su único dueño (ADR-0013); este servicio la lee al seleccionar y no guarda nada de ella,
/// solo los destinatarios que salen de sus miembros.
/// </summary>
/// <param name="Id">Identidad de la lista en el CRM.</param>
/// <param name="OptOfficeId">La OPT dueña. Solo su personal puede usar la lista.</param>
/// <param name="ContactIds">Sus miembros vivos: el CRM ya quitó los borrados y los suprimidos por derecho al olvido. Los dados de baja siguen, y la selección los excluye como a cualquiera.</param>
public sealed record CrmContactList(Guid Id, Guid OptOfficeId, IReadOnlyList<Guid> ContactIds);
