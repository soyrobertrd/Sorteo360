using Microsoft.AspNetCore.Http;

namespace Sorteo360.Convocatorias.Web.Routing;

/// <summary>
/// Los dos únicos prefijos de ruta del servicio, ambos versionados. Uno por servicio y no
/// uno por recurso: la separación que importa acá no es qué recurso se toca sino quién
/// puede tocarlo.
/// <para>
/// <see cref="PublicPrefix"/> es tráfico anónimo de internet —el portal de registro— y es
/// el único lugar donde vive un <c>AllowAnonymous</c> de dominio. <see cref="InternalPrefix"/>
/// es todo lo demás y siempre exige un usuario autenticado.
/// </para>
/// </summary>
public static class ApiRoutes
{
    /// <summary>Prefijo anónimo, tal como se escribe al mapear un grupo.</summary>
    public const string PublicPrefix = "v{version:apiVersion}/public";

    /// <summary>Prefijo interno, tal como se escribe al mapear un grupo.</summary>
    public const string InternalPrefix = "v{version:apiVersion}/convocatorias";

    /// <summary>
    /// Si la URL de una petición cae bajo el prefijo público. Lo pregunta el limitador de
    /// peticiones, que corre sobre la ruta y no sobre el endpoint. Se mira el segundo
    /// segmento en vez de comparar contra <c>/v1/public</c> para que una v2 no deje el
    /// portal sin límite el día que exista.
    /// </summary>
    public static bool IsPublicPath(PathString path)
    {
        if (!path.HasValue)
            return false;

        var segments = path.Value!.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2
            && IsVersionSegment(segments[0])
            && segments[1].Equals("public", StringComparison.Ordinal);
    }

    /// <summary>Una <c>v</c> seguida de dígitos y nada más, para que <c>/version/...</c> no cuente.</summary>
    private static bool IsVersionSegment(string segment)
    {
        if (segment.Length < 2 || segment[0] is not ('v' or 'V'))
            return false;

        return segment.AsSpan(1).ContainsAnyExcept("0123456789") is false;
    }
}
