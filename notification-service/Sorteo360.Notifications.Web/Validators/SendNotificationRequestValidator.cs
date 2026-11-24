using System.Text.RegularExpressions;
using FluentValidation;
using Sorteo360.Notifications.Core.Notifications;
using Sorteo360.Notifications.Web.NotificationEndpoints;

namespace Sorteo360.Notifications.Web.Validators;

/// <summary>
/// La forma del contrato: lo que tiene que venir y cómo. Todo lo que este validador rechaza se
/// contesta 400 con el código de payload inválido y sin haber guardado una sola fila.
/// <para>
/// Lo que NO valida acá es la dirección de cada persona: una dirección inválida no tumba la
/// petición entera, se contesta por persona dentro del 202 y sin fila. Tampoco el tope de
/// personas ni los marcadores, que llevan código propio y viven en el servicio.
/// </para>
/// </summary>
public sealed partial class SendNotificationRequestValidator : AbstractValidator<SendNotificationRequest>
{
    public SendNotificationRequestValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(type => TypePattern().IsMatch(type!))
            .When(x => !string.IsNullOrWhiteSpace(x.Type))
            .WithMessage("El type se escribe en minúsculas y con puntos: servicio.cosa.que-pasó.");

        RuleFor(x => x.Language)
            .NotEmpty()
            .Must(language => AllowedLanguage.All.Contains(language!))
            .When(x => !string.IsNullOrWhiteSpace(x.Language))
            .WithMessage($"El idioma debe ser uno de: {string.Join(", ", AllowedLanguage.All)}.");

        RuleFor(x => x.FromAddress)
            .NotEmpty()
            .Must(address => EmailAddressSyntax.IsValid(address))
            .When(x => !string.IsNullOrWhiteSpace(x.FromAddress))
            .WithMessage("El fromAddress no es una dirección de correo.");

        RuleFor(x => x.Subject)
            .NotEmpty();

        RuleFor(x => x.Body)
            .NotEmpty();

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty();

        RuleFor(x => x.Recipients)
            .NotNull()
            .NotEmpty()
            .WithMessage("El envío necesita al menos una persona.");

        // La dirección puede ser cualquier cosa (se contesta por persona), pero la entrada tiene que
        // existir: un null en la lista no es una persona.
        RuleForEach(x => x.Recipients)
            .NotNull()
            .When(x => x.Recipients is not null);

        // El nombre es opcional; lo que sí se exige es que quepa. Se mide ya recortado, que es como
        // se guarda.
        RuleForEach(x => x.Recipients)
            .Must(recipient => recipient?.Name is null || recipient.Name.Trim().Length <= NotificationRecipient.NameMaxLength)
            .When(x => x.Recipients is not null)
            .WithMessage($"El nombre de una persona no puede pasar de {NotificationRecipient.NameMaxLength} caracteres.");

        RuleFor(x => x.About)
            .NotNull()
            .NotEmpty()
            .WithMessage("El envío tiene que decir de qué trata.");

        RuleForEach(x => x.About)
            .NotNull()
            .ChildRules(about =>
            {
                about.RuleFor(entry => entry!.SourceService).NotEmpty();
                about.RuleFor(entry => entry!.EntityType).NotEmpty();
                about.RuleFor(entry => entry!.EntityId).NotEmpty();
            })
            .When(x => x.About is not null);
    }

    /// <summary>
    /// Minúsculas, dígitos y guiones, en tres segmentos o más separados por puntos. El prefijo del
    /// servicio no es decoración: el espacio de nombres es de toda la plataforma.
    /// </summary>
    [GeneratedRegex(@"^[a-z0-9-]+(\.[a-z0-9-]+){2,}$", RegexOptions.CultureInvariant)]
    private static partial Regex TypePattern();
}
