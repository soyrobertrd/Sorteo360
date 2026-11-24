using FluentValidation;
using Sorteo360.Events.Web.RaffleEndpoints;

namespace Sorteo360.Events.Web.Validators;

public sealed class RemoveAttendeeOverrideRequestValidator : AbstractValidator<RemoveAttendeeOverrideRequest>
{
    public RemoveAttendeeOverrideRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("El correo es obligatorio.")
            .EmailAddress()
            .WithMessage("El correo no tiene un formato válido.");
    }
}
