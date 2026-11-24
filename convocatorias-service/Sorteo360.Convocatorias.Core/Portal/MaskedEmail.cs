namespace Sorteo360.Convocatorias.Core.Portal;

/// <summary>
/// La dirección de una persona tal como la enseña la página de baja: la primera letra, tres
/// asteriscos y el dominio entero, <c>a***@acme.com</c> (spec <c>06</c> §2.4).
/// <para>
/// <b>Por qué enmascarar y no callar.</b> Un token de baja es distinto del de registro, pero
/// quien recibe un reenvío recibe el correo entero, enlace de baja incluido. Nombrar a quién va
/// a dar de baja —sin revelar la dirección completa— es lo que hace que el que reenvió vea una
/// que no es la suya y no siga.
/// </para>
/// <para>
/// <b>Siempre una sola letra visible.</b> Dejar más al descubierto en las direcciones largas
/// filtraría más cuanto más larga es, que es justo al revés de lo que hace falta.
/// </para>
/// </summary>
public static class MaskedEmail
{
    private const string LocalMask = "***";

    /// <summary>
    /// Enmascara <paramref name="email"/>. Espera una dirección con forma —es la que se guardó al
    /// enviarle, nunca nula—, así que la arroba está: la primera letra sobrevive, el resto del
    /// buzón se tapa y el dominio queda tal cual, que es lo que deja reconocer la propia sin
    /// enseñar la ajena.
    /// </summary>
    public static string Of(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var trimmed = email.Trim();
        var at = trimmed.IndexOf('@', StringComparison.Ordinal);

        // Sin arroba no hay buzón ni dominio que separar; se tapa entero antes que arriesgar
        // enseñar de más. No debería llegar acá: la dirección viene de una fila ya validada.
        if (at <= 0)
            return LocalMask;

        return $"{trimmed[0]}{LocalMask}{trimmed[at..]}";
    }
}
