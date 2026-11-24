using Microsoft.EntityFrameworkCore;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Core.Identity;

namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Contexto de base de datos para el servicio de eventos.
/// Trabaja con las clases de persistencia internas (<see cref="EventData"/>,
/// <see cref="RoadshowCityData"/>); las entidades de dominio se convierten
/// en el repositorio.
/// </summary>
public sealed class EventsDbContext : DbContext
{
    private readonly ICurrentAccount? _currentAccount;

    public Guid CurrentAccountId => _currentAccount?.AccountId ?? Account.DefaultBanreservasAccountId;
    internal DbSet<EventData> Events { get; set; } = null!;

    internal DbSet<RoadshowCityData> RoadshowCities { get; set; } = null!;

    internal DbSet<EventImageData> EventImages { get; set; } = null!;

    internal DbSet<StoredFileData> StoredFiles { get; set; } = null!;

    internal DbSet<RaffleData> Raffles { get; set; } = null!;

    internal DbSet<RaffleRoundData> RaffleRounds { get; set; } = null!;

    internal DbSet<PrizeData> Prizes { get; set; } = null!;

    internal DbSet<RaffleAttendeeOverrideData> RaffleAttendeeOverrides { get; set; } = null!;

    internal DbSet<RaffleAttendeeData> RaffleAttendees { get; set; } = null!;

    internal DbSet<RaffleParticipantData> RaffleParticipants { get; set; } = null!;

    internal DbSet<BrandingSettingData> BrandingSettings { get; set; } = null!;

    internal DbSet<FeatureSwitchData> FeatureSwitches { get; set; } = null!;

    internal DbSet<AccountData> Accounts { get; set; } = null!;

    internal DbSet<PlanRequestData> PlanRequests { get; set; } = null!;

    internal DbSet<AccountActivationData> AccountActivations { get; set; } = null!;

    internal DbSet<AccountMonthlyUsageData> AccountMonthlyUsages { get; set; } = null!;

    internal DbSet<PrizeDrawData> PrizeDraws { get; set; } = null!;

    internal DbSet<PrizeHandoverData> PrizeHandovers { get; set; } = null!;

    internal DbSet<NotarialRecordData> NotarialRecords { get; set; } = null!;

    /// <summary>
    /// Las plantillas de correo de este servicio, sembradas por una migración de datos. Catálogo
    /// de solo lectura en fase 1.
    /// </summary>
    internal DbSet<NotificationTemplateData> NotificationTemplates { get; set; } = null!;

    public EventsDbContext(DbContextOptions<EventsDbContext> options, ICurrentAccount? currentAccount = null)
        : base(options)
    {
        _currentAccount = currentAccount;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventsDbContext).Assembly);

        // Aislamiento por cuenta (§9.4, R7, S4): eventos, marcas y archivos de otra cuenta quedan ocultos (404).
        modelBuilder.Entity<EventData>()
            .HasQueryFilter(e => e.DeletedAt == null && e.AccountId == CurrentAccountId);

        modelBuilder.Entity<BrandingSettingData>()
            .HasQueryFilter(b => b.AccountId == CurrentAccountId);

        modelBuilder.Entity<StoredFileData>()
            .HasQueryFilter(f => f.AccountId == CurrentAccountId);

        // Las imágenes no llevan cuenta: la heredan de su evento, que ya está filtrado. Sin esto,
        // DELETE events/{id}/images/{imageId} borraba la imagen de un evento de otra cuenta
        // (auditoría run-1, pendiente 5). Reemplaza el filtro de EventImageConfiguration, así que
        // repite el deleted_at.
        modelBuilder.Entity<EventImageData>()
            .HasQueryFilter(i => i.DeletedAt == null && Events.Any(e => e.Id == i.EventId));
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // S4: estampar la cuenta actual en cada StoredFileData nuevo cuando hay cuenta activa en la petición.
        if (_currentAccount?.TryAccountId is { } accountId && accountId != Guid.Empty)
        {
            foreach (var entry in ChangeTracker.Entries<StoredFileData>())
            {
                if (entry.State == EntityState.Added)
                    entry.Entity.AccountId = accountId;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
