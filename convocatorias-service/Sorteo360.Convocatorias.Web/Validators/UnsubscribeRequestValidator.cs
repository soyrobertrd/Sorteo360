using FluentValidation;
using Sorteo360.Convocatorias.Web.PublicEndpoints;

namespace Sorteo360.Convocatorias.Web.Validators;

/// <summary>
/// Darse de baja. Lo único que se comprueba acá es que venga algo y que no sea absurdamente
/// largo: si el token sirve o no lo dice el servicio, y siempre con el mismo <c>404</c>. El tope
/// se comparte con las otras rutas del portal para que no acepte una lo que otra rechaza.
/// </summary>
public sealed class UnsubscribeRequestValidator : AbstractValidator<UnsubscribeRequest>
{
    public UnsubscribeRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage(ValidationMessages.Required)
            .MaximumLength(PortalTokenField.MaximumLength).WithMessage(ValidationMessages.MaximumLength);
    }
}
