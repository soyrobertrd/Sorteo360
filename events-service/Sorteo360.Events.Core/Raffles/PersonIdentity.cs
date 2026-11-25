namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// La clave de una persona dentro de un evento (<c>identity_key</c>): el correo normalizado si lo
/// tiene y, si no, <c>id:</c> más su ID de empleado recortado y en minúsculas. Es interna: no se
/// muestra ni se usa como dirección. Las filas viejas sin correo ni ID llevan <c>legacy:{id}</c>,
/// puesta por la migración que quitó los correos inventados.
/// </summary>
public static class PersonIdentity
{
    public const string IdPrefix = "id:";
    public const string LegacyPrefix = "legacy:";

    /// <summary>La clave de la persona, o nulo si no trae correo ni ID: esa fila no se puede cargar.</summary>
    public static string? KeyFor(string? email, string? employeeCode)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail is not null)
            return normalizedEmail;

        var normalizedId = NormalizeId(employeeCode);
        return normalizedId is null ? null : IdPrefix + normalizedId;
    }

    /// <summary>El correo que lleva la clave, o nulo si la clave es de un ID o de una fila vieja.</summary>
    public static string? EmailOf(string identityKey)
    {
        ArgumentNullException.ThrowIfNull(identityKey);
        return identityKey.StartsWith(IdPrefix, StringComparison.Ordinal)
            || identityKey.StartsWith(LegacyPrefix, StringComparison.Ordinal)
            ? null
            : identityKey;
    }

    /// <summary>El ID de empleado que lleva la clave, o nulo si no es una clave de ID.</summary>
    public static string? IdOf(string identityKey)
    {
        ArgumentNullException.ThrowIfNull(identityKey);
        return identityKey.StartsWith(IdPrefix, StringComparison.Ordinal) ? identityKey[IdPrefix.Length..] : null;
    }

    /// <summary>Sin espacios alrededor y en minúsculas; nulo si viene en blanco.</summary>
    public static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    /// <summary>Sin espacios alrededor y en minúsculas; nulo si viene en blanco.</summary>
    public static string? NormalizeId(string? employeeCode) =>
        string.IsNullOrWhiteSpace(employeeCode) ? null : employeeCode.Trim().ToLowerInvariant();
}
