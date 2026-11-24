namespace Sorteo360.Events.Infrastructure.Notifications;

/// <summary>
/// Datos bancarios para transferencias de planes pagos y correo del superadministrador (§9.5).
/// </summary>
public sealed class PlanBillingSettings
{
    public const string SectionName = "PlanBilling";

    public string BankName { get; set; } = "Banreservas";
    public string AccountHolder { get; set; } = "Sorteo360";
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountType { get; set; } = "Cuenta corriente";
    public string? TaxId { get; set; }
    public string? Instructions { get; set; } = "Incluye el nombre de tu empresa como referencia de la transferencia.";
    public string? PlatformAdminEmail { get; set; }
}

