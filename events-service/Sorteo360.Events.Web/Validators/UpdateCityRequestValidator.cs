using FluentValidation;
using Sorteo360.Events.Web.CityEndpoints;

namespace Sorteo360.Events.Web.Validators;

public sealed class UpdateCityRequestValidator : AbstractValidator<UpdateCityRequest>
{
    public UpdateCityRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty();

        RuleFor(x => x.Country)
            .NotEmpty();

        // Solo se rechaza la ausencia: un id presente pero desconocido lo rechaza el servicio con
        // TIME_ZONE_UNKNOWN, porque la respuesta de este validador no lleva código.
        RuleFor(x => x.TimeZone)
            .NotNull();

        RuleFor(x => x.DisplayOrder)
            .GreaterThan(0)
            .When(x => x.DisplayOrder is not null);
    }
}
