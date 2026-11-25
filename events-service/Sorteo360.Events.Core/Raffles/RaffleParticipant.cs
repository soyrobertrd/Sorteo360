namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Participante registrado en el padrón del sorteo de un evento, persistido independientemente de
/// si ya inició el sorteo o si llegó desde archivo, captura manual, API o webhook.
/// </summary>
public sealed record RaffleParticipant
{
    public Guid Id { get; init; }
    public Guid EventId { get; init; }
    public string FullName { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    /// <summary>El correo normalizado, o nulo si la persona entró solo con su ID.</summary>
    public string? Email { get; init; }

    /// <summary>La clave de la persona en el evento (<see cref="PersonIdentity"/>). Nunca se muestra.</summary>
    public string IdentityKey { get; init; }
    public string? Company { get; init; }
    public string? Department { get; init; }
    public string? Location { get; init; }
    public string? EmployeeCode { get; init; }
    public string? Phone { get; init; }
    public string QrCode { get; init; }
    public string TicketNumber { get; init; }
    public bool CheckedIn { get; init; }
    public DateTimeOffset? CheckedInAt { get; init; }
    public string Source { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Clave de agrupación para la regla "un premio por empresa/departamento" (R1): usa siempre
    /// <see cref="Department"/> si está presente y solo cae a <see cref="Company"/> si no hay departamento.
    /// Compartida por <c>RosterAttendanceSource</c> y el DTO del padrón para que la tómbola y la UI coincidan.
    /// </summary>
    public string? RuleGroup => ResolveRuleGroup(Department, Company);

    public static string? ResolveRuleGroup(string? department, string? company) =>
        !string.IsNullOrWhiteSpace(department) ? department.Trim()
        : !string.IsNullOrWhiteSpace(company) ? company.Trim()
        : null;

    public RaffleParticipant(
        Guid id,
        Guid eventId,
        string fullName,
        string? email,
        string? company,
        string? department,
        string? employeeCode,
        string? phone,
        string qrCode,
        bool checkedIn,
        DateTimeOffset? checkedInAt,
        string source,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        string ticketNumber = "",
        string? firstName = null,
        string? lastName = null,
        string? location = null,
        string? identityKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(qrCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        Id = id;
        EventId = eventId;
        FullName = fullName;
        FirstName = firstName;
        LastName = lastName;
        Email = PersonIdentity.NormalizeEmail(email);
        // Sin clave explícita se arma del correo o del ID; sin ninguno de los dos la fila no existe.
        IdentityKey = identityKey ?? PersonIdentity.KeyFor(Email, employeeCode)
            ?? throw new ArgumentException("El participante necesita correo o ID.", nameof(email));
        Company = company;
        Department = department;
        Location = location;
        EmployeeCode = employeeCode;
        Phone = phone;
        QrCode = qrCode;
        TicketNumber = ticketNumber;
        CheckedIn = checkedIn;
        CheckedInAt = checkedInAt;
        Source = source;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }
}

