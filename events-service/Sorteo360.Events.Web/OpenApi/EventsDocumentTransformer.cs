using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Sorteo360.Platform.Auth;

namespace Sorteo360.Events.Web.OpenApi;

/// <summary>
/// Transforma el documento OpenAPI generado de forma nativa por .NET 10 para
/// enriquecer la metadata (título, versión y descripción) que muestra la UI de Scalar,
/// y para describir en español cada tag. Lo registra Program.cs al componer el
/// documento del chasis.
/// La prosa va en español y los identificadores en inglés.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class EventsDocumentTransformer : IOpenApiDocumentTransformer
{
    private const string DocumentDescription =
        """
        API del servicio de eventos y sorteos corporativos de la plataforma Sorteo360:
        planificación de eventos, gestión e importación de participantes, check-in por
        código QR, integración por API y webhooks, y ejecución auditable de sorteos.

        La documentación está en español; el contrato es en inglés. Rutas, campos,
        esquemas y códigos almacenados (`draft`, `in_progress`, `completed`) aparecen
        tal como viajan en la API.
        """;

    /// <summary>
    /// Descripción en español de cada tag. Los nombres son los identificadores del
    /// contrato y no se traducen.
    /// </summary>
    private static readonly (string Name, string Description)[] TagDescriptions =
    [
        ("Events",
            """
            Los eventos y su ciclo de vida: se crean en `planning`, se inician a `in_progress` y
            terminan cerrados o cancelados.
            """),

        ("Cities",
            """
            Las ciudades o ubicaciones de un evento, cada una con su fecha y su orden de
            presentación.
            """),

        ("Files",
            """
            Los archivos asociados a eventos y sorteos (imágenes, logos y actas). Se suben al recurso
            que los lleva y se leen por id, así que cualquier consumidor que tenga uno puede pedir su
            ficha o sus bytes.
            """)
    ];

    /// <summary>
    /// Ajusta la sección <c>Info</c> del documento OpenAPI y describe sus tags antes
    /// de exponerlo.
    /// </summary>
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);

        document.Info = new OpenApiInfo
        {
            Title = "Sorteo360 — Events & Raffles API",
            Version = context.DocumentName,
            Description = DocumentDescription,
        };

        DescribeTags(document);

        document.AddKeycloakBearerSecurity();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Aplica la descripción en español a cada tag del documento, creando el tag si el
    /// generador todavía no lo materializó.
    /// </summary>
    private static void DescribeTags(OpenApiDocument document)
    {
        document.Tags ??= new HashSet<OpenApiTag>();

        foreach (var (name, description) in TagDescriptions)
        {
            var existing = document.Tags.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.Ordinal));

            if (existing is not null)
                existing.Description = description;
            else
                document.Tags.Add(new OpenApiTag { Name = name, Description = description });
        }
    }
}
