using FluentValidation;
using Sorteo360.Convocatorias.Web.CheckInEndpoints;

namespace Sorteo360.Convocatorias.Web.Validators;

/// <summary>
/// Al menos un contacto y como mucho <see cref="MaxContactIds"/>. Sin ninguno, la lectura sería
/// "todas las asistencias del servicio", que nadie pidió.
/// </summary>
public sealed class ListContactCheckInsRequestValidator : AbstractValidator<ListContactCheckInsRequest>
{
    /// <summary>
    /// Lo que cabe en una línea de petición: cien GUID son unos 4,6 KB de consulta, por debajo de
    /// los 8 KB que nginx acepta. Es además la página más grande del listado de contactos.
    /// </summary>
    public const int MaxContactIds = 100;

    private static readonly string TooManyContacts =
        ValidationMessages.MaximumItems.Replace("{MaxItems}", MaxContactIds.ToString());

    public ListContactCheckInsRequestValidator()
    {
        RuleFor(x => x.ContactIds)
            .NotEmpty().WithMessage(ValidationMessages.Required);

        RuleFor(x => x.ContactIds)
            .Must(ids => ids!.Length <= MaxContactIds).WithMessage(TooManyContacts)
            .When(x => x.ContactIds is not null);

        RuleForEach(x => x.ContactIds)
            .NotEmpty().WithMessage(ValidationMessages.EmptyIdentifier);
    }
}
