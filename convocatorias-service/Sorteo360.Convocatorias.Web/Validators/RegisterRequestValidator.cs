using FluentValidation;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Web.PublicEndpoints;

namespace Sorteo360.Convocatorias.Web.Validators;

/// <summary>
/// El formulario del portal (spec <c>06</c> §4.1).
/// <para>
/// <b>Obligatorios son tres y no siete.</b> Nombre, apellido y correo son lo que el alta de un
/// contacto en el CRM exige y lo único sin lo que no hay inscripción. Teléfono, empresa, cargo y
/// país se piden en la pantalla y se guardan si vienen: negarle la entrada a alguien en un
/// aeropuerto porque dejó el cargo en blanco no es lo que la spec está pidiendo.
/// </para>
/// </summary>
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    /// <summary>Lo que cabe en la parte local más la arroba más el dominio de un correo.</summary>
    private const int EmailMaximumLength = 320;

    /// <summary>Un campo de texto del formulario. Holgado: recorta un pegado, no un nombre.</summary>
    private const int TextMaximumLength = 200;

    public RegisterRequestValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty().WithMessage(ValidationMessages.Required);

        RuleFor(x => x.Token)
            .MaximumLength(PortalTokenField.MaximumLength).WithMessage(ValidationMessages.MaximumLength);

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage(ValidationMessages.Required)
            .MaximumLength(TextMaximumLength).WithMessage(ValidationMessages.MaximumLength);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage(ValidationMessages.Required)
            .MaximumLength(TextMaximumLength).WithMessage(ValidationMessages.MaximumLength);

        // La misma regla de forma que usan la carga de listas y la entrega de un lote, no una
        // copia suya: una dirección aceptada por una y rechazada por otra es cómo alguien se
        // inscribe y después nunca recibe nada.
        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ValidationMessages.Required)
            .MaximumLength(EmailMaximumLength).WithMessage(ValidationMessages.MaximumLength)
            .Must(EmailAddressShape.IsWellFormed).WithMessage(ValidationMessages.InvalidEmail);

        // La forma que el CRM acepta: mejor que la persona lo corrija aquí que dejar la
        // inscripción sin poder sincronizarse.
        RuleFor(x => x.Phone)
            .MaximumLength(TextMaximumLength).WithMessage(ValidationMessages.MaximumLength)
            .Must(PhoneShape.IsAcceptedByCrm).WithMessage(ValidationMessages.InvalidPhone);

        RuleFor(x => x.Company)
            .MaximumLength(TextMaximumLength).WithMessage(ValidationMessages.MaximumLength);

        RuleFor(x => x.JobTitle)
            .MaximumLength(TextMaximumLength).WithMessage(ValidationMessages.MaximumLength);

        RuleFor(x => x.Country)
            .MaximumLength(TextMaximumLength).WithMessage(ValidationMessages.MaximumLength);
    }
}
