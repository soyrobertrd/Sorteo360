using Sorteo360.Events.Web.Filters;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>Lee las cabeceras de estado del directorio de una respuesta.</summary>
internal static class CrmDirectoryStatusHeaderExtensions
{
    /// <summary>
    /// Exige el estado en las dos cabeceras a la vez. Son un solo valor con dos nombres, así que
    /// afirmarlas por separado dejaría pasar justo el fallo que importa: que se desincronicen.
    /// </summary>
    public static void ShouldReportDirectoryStatus(this HttpResponseMessage response, string expected)
    {
        response.DirectoryStatus(CrmDirectoryStatusFilter.HeaderName).ShouldBe(expected);
        response.DirectoryStatus(CrmDirectoryStatusFilter.OptHeaderName).ShouldBe(expected);
    }

    /// <summary>El valor de una de las dos cabeceras, o <c>null</c> si la respuesta no la trae.</summary>
    public static string? DirectoryStatus(this HttpResponseMessage response, string headerName) =>
        response.Headers.TryGetValues(headerName, out var values)
            ? values.SingleOrDefault()
            : null;
}
