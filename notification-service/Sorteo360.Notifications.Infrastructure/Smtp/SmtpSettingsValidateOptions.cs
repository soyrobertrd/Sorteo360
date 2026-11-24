using FluentValidation;
using Microsoft.Extensions.Options;
using Sorteo360.Notifications.Core.Sending;

namespace Sorteo360.Notifications.Infrastructure.Smtp;

/// <summary>
/// Pone <see cref="SmtpSettingsValidator"/> donde el arranque lo mira. Sin esto la validación de
/// FluentValidation quedaría escrita y nunca se ejecutaría: las opciones solo consultan
/// <see cref="IValidateOptions{TOptions}"/>.
/// </summary>
internal sealed class SmtpSettingsValidateOptions : IValidateOptions<SmtpSettings>
{
    private readonly IValidator<SmtpSettings> _validator;

    public SmtpSettingsValidateOptions(IValidator<SmtpSettings> validator)
    {
        ArgumentNullException.ThrowIfNull(validator);

        _validator = validator;
    }

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SmtpSettings options)
    {
        var result = _validator.Validate(options);

        return result.IsValid
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(result.Errors.Select(failure => failure.ErrorMessage));
    }
}
