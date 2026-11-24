using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// La lista cambió debajo de quien lanza: la cuenta que traía la petición no es la que tiene la
/// campaña ahora. Quien lanza relee y vuelve a lanzar; no se manda nada y no se escribe nada
/// (spec <c>03</c> §5.1).
/// <para>
/// Lleva la cuenta real en una extensión para que la pantalla pueda decir "esto eran 227 cuando
/// lo abriste y ahora son 240". Se descartaron un token de versión y un <c>If-Match</c>: lo que
/// cambia no es la fila de la campaña sino cuántas filas hay en otra tabla, así que un sello de
/// versión sobre <c>campaign</c> tendría que bumpearlo cada escritura de destinatario para
/// significar algo, y quien coordina entiende "227 pasaron a 240" como no entiende jamás un
/// ETag opaco.
/// </para>
/// </summary>
public sealed record CampaignRecipientsChangedError : BaseError
{
    public const string ErrorCode = "CAMPAIGN_RECIPIENTS_CHANGED";

    private const string ExpectedExtension = "expectedRecipientCount";
    private const string ActualExtension = "actualRecipientCount";

    /// <summary>La cuenta que traía la petición.</summary>
    public int Expected { get; init; }

    /// <summary>La que tiene la campaña ahora.</summary>
    public int Actual { get; init; }

    public CampaignRecipientsChangedError(int expected, int actual)
        : base(
            $"La lista cambió: esperabas {expected} destinatarios y ahora son {actual}.",
            ErrorCode,
            ErrorKind.Conflict)
    {
        Expected = expected;
        Actual = actual;
    }

    public override IReadOnlyDictionary<string, object?> ProblemExtensions =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [ExpectedExtension] = Expected,
            [ActualExtension] = Actual
        };
}
