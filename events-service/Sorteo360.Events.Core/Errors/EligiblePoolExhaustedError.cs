using Sorteo360.Events.Core.Raffles;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Errors;

/// <summary>
/// No queda nadie que pueda ganar el premio con las reglas del sorteo, y no se escribe nada: un sorteo
/// que no eligió a nadie no ocurrió (spec 02 §2.1). Las reglas no ceden; el operador puede dejar el
/// premio sin sortear.
/// <para>
/// Lleva las cuentas que lo explican, que suman <c>participants</c>: quien ve
/// <c>excludedByPerson: 11</c> sabe por qué sin preguntarle a nadie.
/// </para>
/// </summary>
public sealed record PoolExhaustedError : BaseError
{
    public const string ErrorCode = "RAFFLE_POOL_EXHAUSTED";

    public int Participants { get; init; }
    public int ExcludedByVoid { get; init; }
    public int ExcludedByPerson { get; init; }
    public int ExcludedByCompany { get; init; }
    public IReadOnlyList<object>? Drawn { get; init; }

    public PoolExhaustedError(EligiblePool pool, IReadOnlyList<object>? drawn = null)
        : base(
            "No queda nadie que pueda ganar este premio con las reglas del sorteo.",
            ErrorCode,
            ErrorKind.Conflict)
    {
        ArgumentNullException.ThrowIfNull(pool);

        Participants = pool.Participants;
        ExcludedByVoid = pool.ExcludedByVoid;
        ExcludedByPerson = pool.ExcludedByPerson;
        ExcludedByCompany = pool.ExcludedByCompany;
        Drawn = drawn;
    }

    public override IReadOnlyDictionary<string, object?> ProblemExtensions
    {
        get
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["participants"] = Participants,
                ["excludedByVoid"] = ExcludedByVoid,
                ["excludedByPerson"] = ExcludedByPerson,
                ["excludedByCompany"] = ExcludedByCompany,
            };
            if (Drawn is not null)
                dict["drawn"] = Drawn;
            return dict;
        }
    }
}
