using FluentValidation;
using Sorteo360.Convocatorias.Web.ContactCampaignEndpoints;

namespace Sorteo360.Convocatorias.Web.Validators;

/// <summary>
/// Al menos un contacto y como mucho los mismos cien que la lectura de asistencias, por la misma
/// razón: es lo que cabe en una línea de petición.
/// </summary>
public sealed class ListContactCampaignsRequestValidator : AbstractValidator<ListContactCampaignsRequest>
{
    private static readonly string TooManyContacts = ValidationMessages.MaximumItems
        .Replace("{MaxItems}", ListContactCheckInsRequestValidator.MaxContactIds.ToString());

    public ListContactCampaignsRequestValidator()
    {
        RuleFor(x => x.ContactIds)
            .NotEmpty().WithMessage(ValidationMessages.Required);

        RuleFor(x => x.ContactIds)
            .Must(ids => ids!.Length <= ListContactCheckInsRequestValidator.MaxContactIds)
            .WithMessage(TooManyContacts)
            .When(x => x.ContactIds is not null);

        RuleForEach(x => x.ContactIds)
            .NotEmpty().WithMessage(ValidationMessages.EmptyIdentifier);
    }
}
