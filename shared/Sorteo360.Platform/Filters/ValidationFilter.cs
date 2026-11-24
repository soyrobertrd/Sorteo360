using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Sorteo360.Platform.Filters;

/// <summary>
/// Endpoint filter genérico que valida el cuerpo de la petición de tipo <typeparamref name="T"/>
/// con el <see cref="IValidator{T}"/> registrado en el contenedor, antes de ejecutar el handler.
/// Si la validación falla devuelve <c>400</c> con el detalle de los errores y el handler nunca
/// llega a ejecutarse. Centraliza el bloque repetido
/// <c>ValidateAsync</c> + <c>ValidationProblem</c> que antes vivía en cada handler.
/// </summary>
/// <typeparam name="T">Tipo del request a validar (el argumento de cuerpo del handler).</typeparam>
public sealed class ValidationFilter<T> : IEndpointFilter
    where T : class
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // El request es el argumento del handler cuyo tipo es T; el resto son parámetros
        // de ruta y servicios inyectados.
        var request = context.Arguments.OfType<T>().FirstOrDefault();
        if (request is null)
            return await next(context);

        // El validator se resuelve del scope de la petición (no del root) para evitar
        // capturar una dependencia scoped en un filtro construido al arrancar.
        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        if (validator is null)
            return await next(context);

        var validation = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        return await next(context);
    }
}
