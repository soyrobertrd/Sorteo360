using Sorteo360.Events.Core.CrmDirectory;

namespace Sorteo360.Events.Web.Filters;

/// <summary>
/// Emite el estado con el que el CRM resolvió los nombres de esta petición.
/// <para>
/// Va como filtro del grupo entero y no dentro de cada handler para que no se pueda olvidar: un
/// endpoint nuevo que devuelva una referencia al CRM nace con las cabeceras puestas.
/// </para>
/// <para>
/// El valor es un código y no una frase. Este servicio no emite texto para mostrar: el cliente
/// decide qué decir y en qué idioma, y un centinela como "OPT no disponible" metido en un nombre
/// sería prosa dentro del contrato, indistinguible de una oficina que se llamara así.
/// </para>
/// </summary>
public sealed class CrmDirectoryStatusFilter : IEndpointFilter
{
    /// <summary>El nombre que describe lo que la cabecera realmente informa.</summary>
    public const string HeaderName = "X-Crm-Directory-Status";

    /// <summary>
    /// El nombre anterior, cuando el único directorio que se resolvía era el de OPT. Sigue saliendo
    /// con el mismo valor para que las pantallas que ya lo leen no se enteren del cambio; retirarlo
    /// es una decisión del frontend, no de este servicio (ADR-0015).
    /// </summary>
    public const string OptHeaderName = "X-Opt-Directory-Status";

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var result = await next(context);

        // El filtro se resuelve una sola vez al construir la aplicación, así que el estado —que es
        // por petición— se toma del contenedor de la petición y no del constructor.
        var statusContext = context.HttpContext.RequestServices.GetRequiredService<CrmDirectoryStatusContext>();

        // Sin estado informado no hay cabeceras: esta petición no resolvió nada contra el CRM.
        if (statusContext.Status is { } status)
        {
            // Un solo valor con dos nombres. Se escriben juntas a propósito: si se calcularan por
            // separado podrían discrepar, y dos respuestas distintas sobre el mismo hecho es peor
            // que ninguna.
            var headerValue = ToHeaderValue(status);
            context.HttpContext.Response.Headers[HeaderName] = headerValue;
            context.HttpContext.Response.Headers[OptHeaderName] = headerValue;
        }

        return result;
    }

    private static string ToHeaderValue(CrmDirectoryStatus status) => status switch
    {
        CrmDirectoryStatus.Ok => "ok",
        CrmDirectoryStatus.Stale => "stale",
        CrmDirectoryStatus.Unavailable => "unavailable",
        _ => "unavailable"
    };
}
