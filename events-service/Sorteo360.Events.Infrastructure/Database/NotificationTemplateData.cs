namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Fila de <c>template</c>: un correo entero en un idioma. La clave es natural,
/// <c>(template_key, language)</c>, y no hay id sustituto: es un catálogo sembrado que se direcciona
/// por su clave, y un id sería una segunda identidad que nadie usa.
/// </summary>
internal sealed class NotificationTemplateData
{
    public required string TemplateKey { get; set; }

    public required string Language { get; set; }

    public required string Subject { get; set; }

    public required string BodyHtml { get; set; }
}
