namespace Sorteo360.Convocatorias.Core.Crm;

/// <summary>
/// Un contacto del CRM, tal como lo ve el dominio de convocatorias.
/// <para>
/// <b>El CRM no filtra nada de esta lectura.</b> Viene quien no tiene correo y viene quien se
/// dio de baja, cada uno con su <see cref="CanBeEmailed"/> y su <see cref="SuppressionReason"/>.
/// Convocatorias los excluye a la vista, porque quien espera 240 destinatarios y recibe 227 en
/// silencio no tiene forma de encontrar a los 13 que faltan (spec <c>03</c> §6).
/// </para>
/// <para>
/// La empresa no está: <c>companies</c> llega para mostrarse al seleccionar y <b>nunca</b> se
/// copia a la fila del destinatario, porque el nombre de una empresa no es lo que se mandó.
/// </para>
/// </summary>
/// <param name="Id">Identidad, que vive en el CRM.</param>
/// <param name="FirstName">Nombre.</param>
/// <param name="LastName">Apellidos.</param>
/// <param name="Email">Dirección, nula si no tiene.</param>
/// <param name="CanBeEmailed">La respuesta del CRM, calculada por él y nunca por nosotros.</param>
/// <param name="SuppressionReason">Su código de motivo, nulo cuando sí se le puede escribir.</param>
public sealed record CrmContact(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    bool CanBeEmailed,
    string? SuppressionReason);
