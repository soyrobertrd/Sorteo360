using FluentValidation;
using Sorteo360.Events.Web.RaffleEndpoints;

namespace Sorteo360.Events.Web.Validators;

/// <summary>
/// La lista es obligatoria porque reemplaza a la guardada (ADR-0009): omitirla no puede borrar nada
/// por accidente. Cada firmante trae nombre y cargo, medidos ya recortados, que es como se guardan; un
/// firmante a medias es un error, no una fila que se descarta en silencio.
/// </summary>
public sealed class SetCertificateSignersRequestValidator : AbstractValidator<SetCertificateSignersRequest>
{
    // Dos líneas de firma tiene el certificado.
    private const int MaxSigners = 2;

    // El mismo tope que el campo del certificado en pantalla.
    private const int FieldMaxLength = 100;

    public SetCertificateSignersRequestValidator()
    {
        RuleFor(x => x.Signers)
            .NotNull()
            .WithMessage("La lista de firmantes es obligatoria; vacía, los borra.")
            .Must(s => s is null || s.Count <= MaxSigners)
            .WithMessage($"El certificado tiene {MaxSigners} líneas de firma: a lo sumo {MaxSigners} firmantes.");

        RuleForEach(x => x.Signers)
            .NotNull()
            .WithMessage("Un firmante no puede venir vacío.")
            .ChildRules(signer =>
            {
                Required(signer.RuleFor(s => s.Name), "El nombre del firmante");
                Required(signer.RuleFor(s => s.Title), "El cargo del firmante");
            });
    }

    /// <summary>Que el campo venga, no en blanco, y que recortado no pase del tope.</summary>
    private static void Required(IRuleBuilderInitial<CertificateSignerRequest, string?> rule, string field) =>
        rule
            .Must(v => !string.IsNullOrWhiteSpace(v))
            .WithMessage($"{field} es obligatorio.")
            .Must(v => v is null || v.Trim().Length <= FieldMaxLength)
            .WithMessage($"{field} no puede superar {FieldMaxLength} caracteres.");
}
