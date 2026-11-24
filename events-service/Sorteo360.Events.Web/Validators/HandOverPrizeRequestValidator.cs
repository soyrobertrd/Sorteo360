using FluentValidation;
using Sorteo360.Events.Web.RaffleEndpoints;

namespace Sorteo360.Events.Web.Validators;

/// <summary>
/// Los campos se miden ya recortados, que es como se guardan. El documento se pide también cuando
/// quien retira es el ganador: es lo único que deja constancia de que alguien lo vio (spec 03 §3.1).
/// </summary>
public sealed class HandOverPrizeRequestValidator : AbstractValidator<HandOverPrizeRequest>
{
    private const int ReceiverNameMaxLength = 200;
    private const int ReceiverIdentityDocumentMaxLength = 100;

    public HandOverPrizeRequestValidator()
    {
        RuleFor(x => x.ReceiverName)
            .Must(v => !string.IsNullOrWhiteSpace(v))
            .WithMessage("El nombre de quien retira el premio es obligatorio.")
            .Must(v => v is null || v.Trim().Length <= ReceiverNameMaxLength)
            .WithMessage($"El nombre de quien retira no puede superar {ReceiverNameMaxLength} caracteres.");

        RuleFor(x => x.ReceiverIdentityDocument)
            .Must(v => !string.IsNullOrWhiteSpace(v))
            .WithMessage("El documento de identidad de quien retira el premio es obligatorio.")
            .Must(v => v is null || v.Trim().Length <= ReceiverIdentityDocumentMaxLength)
            .WithMessage($"El documento de identidad no puede superar {ReceiverIdentityDocumentMaxLength} caracteres.");

        RuleFor(x => x.ReceiverIsWinner)
            .Must(v => TryParseIsWinner(v, out _))
            .WithMessage("Indique si quien retira es el ganador, con true o false.");

        RuleFor(x => x.Signature)
            .Must(f => f is not null && f.Length > 0)
            .WithMessage("La firma es obligatoria y no puede estar vacía.");
    }

    /// <summary>
    /// <c>true</c> o <c>false</c> en cualquier combinación de mayúsculas, y nada más: ni <c>1</c>, ni
    /// <c>sí</c>, ni espacios alrededor.
    /// </summary>
    public static bool TryParseIsWinner(string? value, out bool isWinner)
    {
        isWinner = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        return isWinner || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
    }
}
