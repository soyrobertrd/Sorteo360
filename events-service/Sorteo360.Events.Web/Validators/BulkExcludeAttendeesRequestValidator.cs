using FluentValidation;
using Sorteo360.Events.Web.RaffleEndpoints;

namespace Sorteo360.Events.Web.Validators;

/// <summary>
/// Solo la nota: las fuentes y el tope los decide el servicio, que responde con sus propios códigos.
/// </summary>
public sealed class BulkExcludeAttendeesRequestValidator : AbstractValidator<BulkExcludeAttendeesRequest>
{
    private const int NoteMaxLength = 1000;

    public BulkExcludeAttendeesRequestValidator()
    {
        RuleFor(x => x.Note)
            .MaximumLength(NoteMaxLength)
            .WithMessage($"La nota no puede superar {NoteMaxLength} caracteres.");
    }
}
