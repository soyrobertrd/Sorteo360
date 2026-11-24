using FluentValidation;
using Sorteo360.Convocatorias.Web.CheckInEndpoints;

namespace Sorteo360.Convocatorias.Web.Validators;

/// <summary>
/// La única comprobación de forma del código (spec <c>09</c> §3.4): que venga y que no sea
/// desmedido. Cualquier otra cosa que no sea de nadie, una URL entera incluida, es un <c>404</c>.
/// </summary>
public sealed class CheckInRequestValidator : AbstractValidator<CheckInRequest>
{
    /// <summary>Holgado para cualquier código que emita el portal, y corto para no leer basura entera.</summary>
    public const int MaxQrCodeLength = 256;

    public CheckInRequestValidator()
    {
        RuleFor(x => x.QrCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ValidationMessages.Required)
            .MaximumLength(MaxQrCodeLength).WithMessage(ValidationMessages.MaximumLength);
    }
}
