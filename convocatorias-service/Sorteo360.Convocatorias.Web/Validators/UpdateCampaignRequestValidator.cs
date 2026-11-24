using FluentValidation;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Web.CampaignEndpoints;

namespace Sorteo360.Convocatorias.Web.Validators;

/// <summary>
/// Es un <c>PATCH</c>, así que los dos campos son opcionales y solo se comprueba lo que viene.
/// Exigir los dos lo volvería un <c>PUT</c>: una pantalla que solo cambia el idioma tendría que
/// reenviar la plantilla para no borrarla, y la que se olvidara la borraría.
/// <para>
/// Lo que sí se rechaza es un campo presente y vacío, que no es "no lo toques" sino "déjalo en
/// blanco", y una campaña sin plantilla no se puede mandar.
/// </para>
/// </summary>
public sealed class UpdateCampaignRequestValidator : AbstractValidator<UpdateCampaignRequest>
{
    public UpdateCampaignRequestValidator()
    {
        RuleFor(x => x.TemplateKey)
            .NotEmpty().WithMessage(ValidationMessages.Required)
            .MaximumLength(100).WithMessage(ValidationMessages.MaximumLength)
            .When(x => x.TemplateKey is not null);

        RuleFor(x => x.Language)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ValidationMessages.Required)
            .Must(language => AllowedLanguage.All.Contains(language!)).WithMessage(ValidationMessages.InvalidLanguage)
            .When(x => x.Language is not null);

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage(ValidationMessages.Required)
            .MaximumLength(CampaignDirectMessage.SubjectMaxLength).WithMessage(ValidationMessages.MaximumLength)
            .When(x => x.Subject is not null);

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage(ValidationMessages.Required)
            .MaximumLength(CampaignDirectMessage.MessageMaxLength).WithMessage(ValidationMessages.MaximumLength)
            .When(x => x.Message is not null);
    }
}
