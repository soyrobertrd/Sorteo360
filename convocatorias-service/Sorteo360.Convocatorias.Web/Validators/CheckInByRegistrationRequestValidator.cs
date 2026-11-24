using FluentValidation;
using Sorteo360.Convocatorias.Web.CheckInEndpoints;

namespace Sorteo360.Convocatorias.Web.Validators;

/// <summary>Que venga un id y que no sea el vacío. De quién es lo decide el servicio.</summary>
public sealed class CheckInByRegistrationRequestValidator : AbstractValidator<CheckInByRegistrationRequest>
{
    public CheckInByRegistrationRequestValidator()
    {
        RuleFor(x => x.RegistrationId)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(ValidationMessages.Required)
            .NotEqual(Guid.Empty).WithMessage(ValidationMessages.EmptyIdentifier);
    }
}
