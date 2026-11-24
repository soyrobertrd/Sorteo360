namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// La entrega de un premio: un formulario multipart con los tres campos de quien retira y la firma
/// (spec 03 §3.1). Todo llega como texto para que el validador, y no el binder, responda cada campo
/// que falte o no se entienda con la misma forma de <c>400</c>.
/// </summary>
public record HandOverPrizeRequest
{
    /// <summary>Quién se llevó el premio, de hasta 200 caracteres. Se recorta.</summary>
    public string? ReceiverName { get; init; }

    /// <summary>Su documento, texto libre de hasta 100 caracteres. Se recorta.</summary>
    public string? ReceiverIdentityDocument { get; init; }

    /// <summary><c>true</c> o <c>false</c>, sin distinguir mayúsculas. Lo declara el operador.</summary>
    public string? ReceiverIsWinner { get; init; }

    /// <summary>La firma, un PNG de hasta 1 MB.</summary>
    public IFormFile? Signature { get; init; }
}
