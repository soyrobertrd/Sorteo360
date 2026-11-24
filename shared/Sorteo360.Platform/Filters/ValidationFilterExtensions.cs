using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Sorteo360.Platform.Filters;

/// <summary>
/// Helpers para enganchar el <see cref="ValidationFilter{TRequest}"/> a una ruta minimal API.
/// </summary>
public static class ValidationFilterExtensions
{
    /// <summary>
    /// Registra un <see cref="ValidationFilter{TRequest}"/> en la ruta, de modo que el cuerpo
    /// de tipo <typeparamref name="TRequest"/> se valide antes de ejecutar el handler, y
    /// declara el 400 correspondiente en el documento OpenAPI. Van juntos a propósito: el
    /// 400 existe exactamente donde hay filtro, así que declararlo aquí lo hace imposible
    /// de olvidar y de documentar donde no ocurre.
    /// </summary>
    /// <typeparam name="TRequest">Tipo del request a validar.</typeparam>
    /// <param name="builder">Builder de la ruta a la que se engancha el filtro.</param>
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder)
        where TRequest : class =>
        builder
            .AddEndpointFilter<ValidationFilter<TRequest>>()
            .ProducesValidationProblem();
}
