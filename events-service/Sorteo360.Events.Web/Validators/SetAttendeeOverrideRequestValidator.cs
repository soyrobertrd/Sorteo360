using FluentValidation;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Web.RaffleEndpoints;

namespace Sorteo360.Events.Web.Validators;

public sealed class SetAttendeeOverrideRequestValidator : AbstractValidator<SetAttendeeOverrideRequest>
{
    private const int NoteMaxLength = 1000;

    public SetAttendeeOverrideRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("El correo es obligatorio.")
            .EmailAddress()
            .WithMessage("El correo no tiene un formato válido.");

        RuleFor(x => x.Decision)
            .NotEmpty()
            .WithMessage("La decisión es obligatoria.")
            .Must(d => d is null || AttendeeOverrideDecision.All.Contains(d))
            .WithMessage($"La decisión debe ser una de: {string.Join(", ", AttendeeOverrideDecision.All)}.");

        RuleFor(x => x.Note)
            .MaximumLength(NoteMaxLength)
            .WithMessage($"La nota no puede superar {NoteMaxLength} caracteres.");
    }
}
