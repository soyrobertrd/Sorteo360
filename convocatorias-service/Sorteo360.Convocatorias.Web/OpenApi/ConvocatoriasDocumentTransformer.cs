using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Sorteo360.Convocatorias.Web.OpenApi;

/// <summary>
/// Transforma el documento OpenAPI generado de forma nativa por .NET 10 para
/// enriquecer la metadata (título, versión y descripción) que muestra la UI de Scalar.
/// Lo registra Program.cs al componer el documento del chasis.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ConvocatoriasDocumentTransformer : IOpenApiDocumentTransformer
{
    /// <summary>
    /// Ajusta la sección <c>Info</c> del documento OpenAPI antes de exponerlo.
    /// </summary>
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "Sorteo360 — Convocatorias API",
            Version = context.DocumentName,
            Description = "API del servicio de Convocatorias de la plataforma Sorteo360.",
        };

        // Nota: cuando se implemente la autenticación (Azure AD / Bearer), aquí se
        // añadirá el security scheme para poder probar endpoints protegidos desde Scalar.

        return Task.CompletedTask;
    }
}
