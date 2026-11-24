using FluentValidation;
using Sorteo360.Notifications.Core.Errors;

namespace Sorteo360.Notifications.Web.Filters;

/// <summary>
/// Valida el cuerpo de la petición y, cuando falla, contesta el 400 con el código de máquina de
/// este servicio junto al detalle por campo.
/// <para>
/// Existe en vez del filtro del chasis porque acá cada fallo lleva código: quien consume ramifica
/// por el código y escribe su propio español, y un 400 sin código lo obligaría a leer la prosa.
/// </para>
/// </summary>
/// <typeparam name="T">Tipo del request a validar.</typeparam>
public sealed class PayloadValidationFilter<T> : IEndpointFilter
    where T : class
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<T>().FirstOrDefault();

        if (request is null)
            return await next(context);

        // El validator sale del scope de la petición, no del raíz: un filtro se construye al
        // arrancar y no puede quedarse con una dependencia scoped.
        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();

        if (validator is null)
            return await next(context);

        var validation = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        if (validation.IsValid)
            return await next(context);

        return Results.ValidationProblem(
            validation.ToDictionary(),
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["code"] = NotificationErrors.PayloadInvalidCode
            });
    }
}

/// <summary>Helpers para enganchar el filtro a una ruta.</summary>
public static class PayloadValidationFilterExtensions
{
    /// <summary>
    /// Engancha el <see cref="PayloadValidationFilter{TRequest}"/> y declara el 400 en el documento
    /// OpenAPI, que es exactamente donde el filtro lo puede producir.
    /// </summary>
    /// <typeparam name="TRequest">Tipo del request a validar.</typeparam>
    /// <param name="builder">Builder de la ruta.</param>
    public static RouteHandlerBuilder WithPayloadValidation<TRequest>(this RouteHandlerBuilder builder)
        where TRequest : class =>
        builder
            .AddEndpointFilter<PayloadValidationFilter<TRequest>>()
            .ProducesValidationProblem();
}
