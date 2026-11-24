namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Campos visibles del participante configurados por sorteo (§10): controla qué se muestra en la
/// consola del operador, el proyector, la animación, la tarjeta del ganador y el historial.
/// No afecta al acta notarial ni al certificado (que siempre llevan nombre completo, boleto e ID).
/// </summary>
public sealed record RaffleDisplayFields(
    string Name,
    string Identifier,
    string Detail)
{
    public const string NameFullName = "full_name";
    public const string NameFirstAndLast = "first_and_last";
    public const string NameFirstName = "first_name";

    public const string IdentifierTicketNumber = "ticket_number";
    public const string IdentifierEmployeeCode = "employee_code";
    public const string IdentifierNone = "none";

    public const string DetailDepartment = "department";
    public const string DetailLocation = "location";
    public const string DetailCompany = "company";
    public const string DetailNone = "none";

    public static readonly IReadOnlySet<string> NameOptions =
        new HashSet<string>(StringComparer.Ordinal) { NameFullName, NameFirstAndLast, NameFirstName };

    public static readonly IReadOnlySet<string> IdentifierOptions =
        new HashSet<string>(StringComparer.Ordinal) { IdentifierTicketNumber, IdentifierEmployeeCode, IdentifierNone };

    public static readonly IReadOnlySet<string> DetailOptions =
        new HashSet<string>(StringComparer.Ordinal) { DetailDepartment, DetailLocation, DetailCompany, DetailNone };

    public static readonly RaffleDisplayFields Default = new(
        NameFullName,
        IdentifierTicketNumber,
        // Bajo el nombre del ganador va su ubicación (sucursal, sede); sin ubicación, el departamento.
        DetailLocation);

    public static bool IsValidName(string? value) => value is not null && NameOptions.Contains(value);
    public static bool IsValidIdentifier(string? value) => value is not null && IdentifierOptions.Contains(value);
    public static bool IsValidDetail(string? value) => value is not null && DetailOptions.Contains(value);

    public static RaffleDisplayFields NormalizeOrDefault(string? name, string? identifier, string? detail) =>
        new(
            IsValidName(name) ? name! : Default.Name,
            IsValidIdentifier(identifier) ? identifier! : Default.Identifier,
            IsValidDetail(detail) ? detail! : Default.Detail);
}

