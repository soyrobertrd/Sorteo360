using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sorteo360.Events.Infrastructure.Database.Configurations;

internal sealed class RaffleParticipantConfiguration : IEntityTypeConfiguration<RaffleParticipantData>
{
    private const int FullNameMaxLength = 200;
    private const int NamePartMaxLength = 120;
    private const int EmailMaxLength = 320;
    private const int CompanyMaxLength = 200;
    private const int DepartmentMaxLength = 200;
    private const int LocationMaxLength = 200;
    private const int EmployeeCodeMaxLength = 100;
    private const int PhoneMaxLength = 50;
    private const int QrCodeMaxLength = 120;
    private const int TicketNumberMaxLength = 7;
    private const int SourceMaxLength = 30;

    public void Configure(EntityTypeBuilder<RaffleParticipantData> builder)
    {
        builder.ToTable("raffle_participant", "events");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id");

        builder.Property(p => p.EventId)
            .HasColumnName("event_id")
            .IsRequired();

        builder.Property(p => p.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(FullNameMaxLength)
            .IsRequired();

        builder.Property(p => p.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(NamePartMaxLength);

        builder.Property(p => p.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(NamePartMaxLength);

        builder.Property(p => p.EmailNormalized)
            .HasColumnName("email_normalized")
            .HasMaxLength(EmailMaxLength)
            .IsRequired();

        builder.Property(p => p.Company)
            .HasColumnName("company")
            .HasMaxLength(CompanyMaxLength);

        builder.Property(p => p.Department)
            .HasColumnName("department")
            .HasMaxLength(DepartmentMaxLength);

        builder.Property(p => p.Location)
            .HasColumnName("location")
            .HasMaxLength(LocationMaxLength);

        builder.Property(p => p.EmployeeCode)
            .HasColumnName("employee_code")
            .HasMaxLength(EmployeeCodeMaxLength);

        builder.Property(p => p.Phone)
            .HasColumnName("phone")
            .HasMaxLength(PhoneMaxLength);

        builder.Property(p => p.QrCode)
            .HasColumnName("qr_code")
            .HasMaxLength(QrCodeMaxLength)
            .IsRequired();

        builder.Property(p => p.TicketNumber)
            .HasColumnName("ticket_number")
            .HasMaxLength(TicketNumberMaxLength)
            .IsRequired();

        builder.Property(p => p.CheckedIn)
            .HasColumnName("checked_in")
            .IsRequired();

        builder.Property(p => p.CheckedInAt)
            .HasColumnName("checked_in_at");

        builder.Property(p => p.Source)
            .HasColumnName("source")
            .HasMaxLength(SourceMaxLength)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasOne<EventData>()
            .WithMany()
            .HasForeignKey(p => p.EventId)
            .HasConstraintName("fk_raffle_participant_event")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.EventId, p.EmailNormalized })
            .HasDatabaseName("ux_raffle_participant_event_email")
            .IsUnique();

        builder.HasIndex(p => new { p.EventId, p.QrCode })
            .HasDatabaseName("ux_raffle_participant_event_qr_code")
            .IsUnique();

        builder.HasIndex(p => new { p.EventId, p.TicketNumber })
            .HasDatabaseName("ux_raffle_participant_event_ticket_number")
            .IsUnique();

        builder.HasIndex(p => new { p.EventId, p.EmployeeCode })
            .HasDatabaseName("ix_raffle_participant_event_employee_code");
    }
}

