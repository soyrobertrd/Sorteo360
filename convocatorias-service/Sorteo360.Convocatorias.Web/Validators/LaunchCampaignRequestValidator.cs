using FluentValidation;
using Sorteo360.Convocatorias.Web.CampaignEndpoints;

namespace Sorteo360.Convocatorias.Web.Validators;

/// <summary>
/// La cuenta del lanzamiento. Obligatoria: omitirla es un <c>400</c>, porque un campo opcional
/// acá fallaría abierto justo en el cliente que más necesita la guarda (spec <c>03</c> §5.1).
/// </summary>
public sealed class LaunchCampaignRequestValidator : AbstractValidator<LaunchCampaignRequest>
{
    public LaunchCampaignRequestValidator()
    {
        RuleFor(x => x.ExpectedRecipientCount)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(ValidationMessages.Required)
            .GreaterThanOrEqualTo(0).WithMessage(ValidationMessages.NotNegative);
    }
}
