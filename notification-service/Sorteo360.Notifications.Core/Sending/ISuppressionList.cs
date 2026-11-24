namespace Sorteo360.Notifications.Core.Sending;

/// <summary>
/// Las direcciones que el proveedor ya reportó muertas u hostiles, alimentada por los eventos de
/// baja y de spam. Se consulta <b>antes de abrir el socket</b>.
/// <para>
/// Esto es entregabilidad y no resolución, y la costura importa: la tubería nunca decide a quién
/// <i>habría que</i> avisar —eso es de quien llama— y solo se niega con direcciones sobre las que
/// el proveedor le dijo algo a ella, porque esa respuesta llega por su webhook y no le llega a
/// nadie más.
/// </para>
/// <para>
/// Un rebote duro no se sincroniza hacia el CRM y no debe: una persona puede estar suprimida en el
/// CRM por consentimiento y acá por buzón, y confundir las dos haría que una dirección mal escrita
/// se viera como un consentimiento retirado, que no se recupera desde la pantalla de
/// consentimiento.
/// </para>
/// </summary>
public interface ISuppressionList
{
    /// <summary>Por qué está suprimida esa dirección, o null si no lo está.</summary>
    Task<string?> ReasonForAsync(string email, CancellationToken cancellationToken);
}
