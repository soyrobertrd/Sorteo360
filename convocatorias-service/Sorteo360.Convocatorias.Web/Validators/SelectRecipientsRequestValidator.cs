using FluentValidation;
using Sorteo360.Convocatorias.Web.CampaignEndpoints;

namespace Sorteo360.Convocatorias.Web.Validators;

/// <summary>
/// El cuerpo de una selección. No exige ningún filtro: sin ninguno el CRM devuelve el
/// directorio entero, que es una convocatoria masiva legítima, y lo que impide mandarla sin
/// querer es la cuenta que hay que repetir al lanzar.
/// </summary>
public sealed class SelectRecipientsRequestValidator : AbstractValidator<SelectRecipientsRequest>
{
    /// <summary>
    /// Tope de valores por dimensión. Cada valor viaja en la cadena de consulta del CRM, y una
    /// lista sin tope construye una URL que su servidor rechaza con un 414 que acá se leería
    /// como "el CRM está caído".
    /// </summary>
    private const int MaxFilterValues = 50;

    /// <summary>El texto de <see cref="ValidationMessages.MaximumItems"/> con el tope ya puesto.</summary>
    private static readonly string TooManyValues =
        ValidationMessages.MaximumItems.Replace("{MaxItems}", MaxFilterValues.ToString());

    /// <summary>
    /// Tope de ids por selección. Es el de la recomprobación del lanzamiento, y la selección es
    /// aditiva, así que una lista más larga entra en dos llamadas.
    /// </summary>
    private const int MaxContactIds = 1000;

    private static readonly string TooManyContactIds =
        ValidationMessages.MaximumItems.Replace("{MaxItems}", MaxContactIds.ToString());

    /// <summary>Si una dimensión del filtro cabe en el tope.</summary>
    private static bool FitsTheFilter(IReadOnlyList<string>? values) => values!.Count <= MaxFilterValues;

    public SelectRecipientsRequestValidator()
    {
        RuleForEach(x => x.Classifications)
            .NotEmpty().WithMessage(ValidationMessages.EmptyIdentifier);

        RuleForEach(x => x.CompanyCategories)
            .NotEmpty().WithMessage(ValidationMessages.EmptyIdentifier);

        RuleFor(x => x.Classifications)
            .Must(FitsTheFilter)
            .WithMessage(TooManyValues)
            .When(x => x.Classifications is not null);

        RuleFor(x => x.CompanyCategories)
            .Must(FitsTheFilter)
            .WithMessage(TooManyValues)
            .When(x => x.CompanyCategories is not null);

        // Una lista vacía no es "sin filtro": eso traería el directorio entero a una campaña
        // pensada para tres personas.
        RuleFor(x => x.ContactIds)
            .NotEmpty().WithMessage(ValidationMessages.Required)
            .Must(ids => ids!.Count <= MaxContactIds).WithMessage(TooManyContactIds)
            .When(x => x.ContactIds is not null);

        RuleForEach(x => x.ContactIds)
            .NotEmpty().WithMessage(ValidationMessages.EmptyIdentifier);

        // Ids y filtro juntos no tienen una lectura obvia (¿Y? ¿O?), así que no se admiten.
        RuleFor(x => x.Classifications)
            .Empty().WithMessage(ValidationMessages.NotWithContactIds)
            .When(x => x.ContactIds is not null);

        RuleFor(x => x.CompanyCategories)
            .Empty().WithMessage(ValidationMessages.NotWithContactIds)
            .When(x => x.ContactIds is not null);

        // La lista de contactos va sola: mezclada con ids o con filtro tampoco tiene una lectura
        // obvia.
        When(x => x.ContactListId is not null, () =>
        {
            RuleFor(x => x.ContactListId).NotEqual(Guid.Empty).WithMessage(ValidationMessages.EmptyIdentifier);
            RuleFor(x => x.ContactIds).Null().WithMessage(ValidationMessages.NotWithContactListId);
            RuleFor(x => x.Classifications).Empty().WithMessage(ValidationMessages.NotWithContactListId);
            RuleFor(x => x.CompanyCategories).Empty().WithMessage(ValidationMessages.NotWithContactListId);
        });
    }
}
