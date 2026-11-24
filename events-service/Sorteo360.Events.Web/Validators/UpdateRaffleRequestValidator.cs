using FluentValidation;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Web.RaffleEndpoints;

namespace Sorteo360.Events.Web.Validators;

public sealed class UpdateRaffleRequestValidator : AbstractValidator<UpdateRaffleRequest>
{
    // Los mismos topes que al recibir un premio: nombre y documento de quien firma.
    private const int NameMaxLength = 200;
    private const int IdentityDocumentMaxLength = 100;

    // El tope de la columna (spec 03 §1).
    private const int PickupInstructionsMaxLength = 1000;

    public UpdateRaffleRequestValidator()
    {
        // Obligatorios a propósito (ADR-0009): sus columnas no admiten nulo, así que omitirlos no
        // tiene a qué borrar y tiene que ser un 400, no un valor adivinado.
        RuleFor(x => x.VisualStyle)
            .NotNull()
            .WithMessage("El estilo visual es obligatorio.")
            .Must(v => v is null || RaffleVisualStyle.All.Contains(v))
            .WithMessage($"El estilo visual debe ser uno de: {string.Join(", ", RaffleVisualStyle.All)}.");

        RuleFor(x => x.ShowPrizeOnScreen)
            .NotNull()
            .WithMessage("Hay que decir si el premio se muestra en pantalla.");

        // El mismo rango que el control del frontend.
        RuleFor(x => x.AnimationSeconds)
            .NotNull()
            .WithMessage("La duración de la animación es obligatoria.")
            .InclusiveBetween(5, 30)
            .WithMessage("La duración de la animación debe estar entre 5 y 30 segundos.");

        RuleFor(x => x.RequiresPresenceConfirmation)
            .NotNull()
            .WithMessage("Hay que decir si se pide confirmar la presencia del ganador.");

        RuleFor(x => x.OnePrizePerPerson)
            .NotNull()
            .WithMessage("Hay que decir si rige la regla de un premio por persona.");

        RuleFor(x => x.OnePrizePerCompany)
            .Must((req, v) => (v ?? req.OnePrizePerDepartmentPerRound) is not null)
            .WithMessage("Hay que decir si rige la regla de un premio por empresa.");

        RuleFor(x => x.BoardBatchSize)
            .InclusiveBetween(1, 10)
            .WithMessage("El tamaño del lote del tablero debe estar entre 1 y 10.")
            .When(x => x.BoardBatchSize.HasValue);

        RuleFor(x => x.BoardIdentifier)
            .Must(v => v is "employee_code" or "ticket")
            .WithMessage("El identificador del tablero debe ser 'employee_code' o 'ticket'.")
            .When(x => !string.IsNullOrWhiteSpace(x.BoardIdentifier));

        // Las autoridades son opcionales mientras el sorteo se arma: se exigen al completarlo.
        RuleFor(x => x.NotaryName)
            .MaximumLength(NameMaxLength)
            .WithMessage($"El nombre del notario no puede superar {NameMaxLength} caracteres.");

        RuleFor(x => x.NotaryIdentityDocument)
            .MaximumLength(IdentityDocumentMaxLength)
            .WithMessage($"El documento del notario no puede superar {IdentityDocumentMaxLength} caracteres.");

        RuleFor(x => x.WitnessOneName)
            .MaximumLength(NameMaxLength)
            .WithMessage($"El nombre del primer testigo no puede superar {NameMaxLength} caracteres.");

        RuleFor(x => x.WitnessOneIdentityDocument)
            .MaximumLength(IdentityDocumentMaxLength)
            .WithMessage($"El documento del primer testigo no puede superar {IdentityDocumentMaxLength} caracteres.");

        RuleFor(x => x.WitnessTwoName)
            .MaximumLength(NameMaxLength)
            .WithMessage($"El nombre del segundo testigo no puede superar {NameMaxLength} caracteres.");

        RuleFor(x => x.WitnessTwoIdentityDocument)
            .MaximumLength(IdentityDocumentMaxLength)
            .WithMessage($"El documento del segundo testigo no puede superar {IdentityDocumentMaxLength} caracteres.");

        // Opcionales como las autoridades: se exigen al completar, que es cuando sale el correo.
        RuleFor(x => x.PickupInstructions)
            .Must(v => v is null || v.Trim().Length <= PickupInstructionsMaxLength)
            .WithMessage($"Las instrucciones de retiro no pueden superar {PickupInstructionsMaxLength} caracteres.");
    }
}
