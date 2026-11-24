using FluentValidation;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Web.CampaignEndpoints;

namespace Sorteo360.Convocatorias.Web.Validators;

public sealed class CreateCampaignRequestValidator : AbstractValidator<CreateCampaignRequest>
{
    public CreateCampaignRequestValidator()
    {
        // Ausente es un mensaje directo; lo que no puede llegar es un evento vacío. Que la plantilla
        // case con la presencia del evento lo decide CampaignDirectMessage, que también ve el PATCH.
        RuleFor(x => x.EventId)
            .NotEqual(Guid.Empty).WithMessage(ValidationMessages.EmptyIdentifier)
            .When(x => x.EventId is not null);

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage(ValidationMessages.Required)
            .MaximumLength(CampaignDirectMessage.SubjectMaxLength).WithMessage(ValidationMessages.MaximumLength)
            .When(x => x.Subject is not null);

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage(ValidationMessages.Required)
            .MaximumLength(CampaignDirectMessage.MessageMaxLength).WithMessage(ValidationMessages.MaximumLength)
            .When(x => x.Message is not null);

        RuleFor(x => x.TemplateKey)
            .NotEmpty().WithMessage(ValidationMessages.Required)
            .MaximumLength(100).WithMessage(ValidationMessages.MaximumLength);

        // El idioma se comprueba contra el catálogo cerrado acá y no solo en el CHECK de la
        // base: un idioma inventado tiene que ser un 400 con su mensaje, no un 500 traducido
        // desde una violación de constraint.
        RuleFor(x => x.Language)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ValidationMessages.Required)
            .Must(language => AllowedLanguage.All.Contains(language!)).WithMessage(ValidationMessages.InvalidLanguage);
    }
}
