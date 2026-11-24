using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Sorteo360.Notifications.Web.OpenApi;

/// <summary>
/// Transforma el documento OpenAPI generado de forma nativa por .NET 10 para
/// enriquecer la metadata (título, versión y descripción) que muestra la UI de Scalar.
/// Lo registra Program.cs al componer el documento del chasis.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class NotificationsDocumentTransformer : IOpenApiDocumentTransformer
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
            Title = "Sorteo360 — Notifications API",
            Version = context.DocumentName,
            Description = "API del servicio de notificaciones de la plataforma Sorteo360. "
                + "Recibe un correo ya redactado y lo entrega; no conoce el dominio de quien lo manda "
                + "ni redacta texto propio.",
        };

        return Task.CompletedTask;
    }
}
