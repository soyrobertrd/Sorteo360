using FluentValidation;
using Sorteo360.Convocatorias.Web.PublicEndpoints;

namespace Sorteo360.Convocatorias.Web.Validators;

/// <summary>
/// Abrir un enlace. Lo único que se comprueba acá es que venga algo y que no sea absurdo: si el
/// token sirve o no lo dice el servicio, y siempre con el mismo <c>404</c>.
/// </summary>
public sealed class ResolveInvitationRequestValidator : AbstractValidator<ResolveInvitationRequest>
{
    public ResolveInvitationRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage(ValidationMessages.Required)
            .MaximumLength(PortalTokenField.MaximumLength).WithMessage(ValidationMessages.MaximumLength);
    }
}
