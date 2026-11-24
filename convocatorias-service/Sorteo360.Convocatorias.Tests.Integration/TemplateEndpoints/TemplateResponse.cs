namespace Sorteo360.Convocatorias.Tests.Integration.TemplateEndpoints;

/// <summary>
/// Lo que el listado de plantillas manda por el cable, leído como lo leería un cliente.
/// <c>BodyHtml</c> está a propósito: un cuerpo que llegara lo atraparía
/// <see cref="TemplateEndpointsTests"/>.
/// </summary>
public sealed record TemplateResponse
{
    public string? TemplateKey { get; init; }

    public string? Language { get; init; }

    public string? Purpose { get; init; }

    public bool IsCampaignSelectable { get; init; }

    public string? Subject { get; init; }

    public string? BodyHtml { get; init; }
}
