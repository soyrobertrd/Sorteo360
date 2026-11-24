using FluentValidation;
using Sorteo360.Events.Web.GlobalEndpoints;

namespace Sorteo360.Events.Web.Validators;

/// <summary>
/// Las reglas de paginación, escritas una vez. Un listado nuevo hereda de acá y solo declara lo
/// suyo; si cada uno las repitiera, el techo del tamaño de página sería el que cada archivo
/// recuerde y no el que el servicio decidió.
/// <para>
/// Ausente (null) significa "aplicá el default", así que no se valida. Presente sí: un 0
/// explícito se rechaza en vez de convertirse en el default por la puerta de atrás.
/// </para>
/// </summary>
/// <typeparam name="T">Request de listado concreto.</typeparam>
public abstract class PagingRequestValidator<T> : AbstractValidator<T>
    where T : PagingRequest
{
    protected PagingRequestValidator()
    {
        RuleFor(x => x.Page!.Value)
            .GreaterThanOrEqualTo(PagingLimits.MinPage)
            .When(x => x.Page is not null);

        RuleFor(x => x.PageSize!.Value)
            .InclusiveBetween(PagingLimits.MinPageSize, PagingLimits.MaxPageSize)
            .When(x => x.PageSize is not null);
    }
}
