using FluentValidation;
using Sorteo360.Events.Web.RaffleEndpoints;

namespace Sorteo360.Events.Web.Validators;

public sealed class RemoveAttendeeOverrideRequestValidator : AbstractValidator<RemoveAttendeeOverrideRequest>
{
    private const int IdentityKeyMaxLength = 330;

    public RemoveAttendeeOverrideRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .When(x => string.IsNullOrWhiteSpace(x.IdentityKey))
            .WithMessage("El correo o la identityKey es obligatorio.");

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email) && string.IsNullOrWhiteSpace(x.IdentityKey))
            .WithMessage("El correo no tiene un formato válido.");

        RuleFor(x => x.IdentityKey)
            .MaximumLength(IdentityKeyMaxLength)
            .WithMessage($"La identityKey no puede superar {IdentityKeyMaxLength} caracteres.");
    }
}
