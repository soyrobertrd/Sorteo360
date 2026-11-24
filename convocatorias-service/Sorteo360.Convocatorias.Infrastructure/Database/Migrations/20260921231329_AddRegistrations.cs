using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Convocatorias.Infrastructure.Database.Migrations
{
    /// <summary>
    /// La tabla de inscripciones y la secuencia de la que sale su <c>reference</c>
    /// (spec <c>06</c> §4). Una fila por persona y por evento, nunca por campaña.
    /// <para>
    /// <b>Las tres unicidades son restricciones y no índices únicos</b>, que en PostgreSQL no es
    /// lo mismo aunque se comporten igual: un <c>CREATE UNIQUE INDEX</c> vive en <c>pg_index</c>
    /// y no aparece en <c>pg_constraint</c>, así que la regla de §4.1 no se podría leer del
    /// catálogo como lo que es.
    /// </para>
    /// <para>
    /// <b>Faltan cuatro columnas de la §4 a propósito</b>, cada una con su ticket: la pareja del
    /// CRM (PGTI-57), <c>cancellation_notified_at</c> (PGTI-62) y los sellos de revisión de
    /// <c>07</c>. La ventana de inscripción (§5) es tabla aparte y llega con PGTI-61.
    /// </para>
    /// </summary>
    public partial class AddRegistrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "registration_reference",
                schema: "convocatorias");

            migrationBuilder.CreateTable(
                name: "registration",
                schema: "convocatorias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    campaign_recipient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    email_normalized = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: true),
                    company = table.Column<string>(type: "text", nullable: true),
                    job_title = table.Column<string>(type: "text", nullable: true),
                    country = table.Column<string>(type: "text", nullable: true),
                    reference = table.Column<string>(type: "text", nullable: false),
                    qr_code = table.Column<string>(type: "text", nullable: false),
                    contact_details_differ = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registration", x => x.id);
                    table.UniqueConstraint("ux_registration_event_email", x => new { x.event_id, x.email_normalized });
                    table.UniqueConstraint("ux_registration_qr_code", x => x.qr_code);
                    table.UniqueConstraint("ux_registration_reference", x => x.reference);
                    table.ForeignKey(
                        name: "FK_registration_campaign_recipient_campaign_recipient_id",
                        column: x => x.campaign_recipient_id,
                        principalSchema: "convocatorias",
                        principalTable: "campaign_recipient",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_registration_campaign_recipient",
                schema: "convocatorias",
                table: "registration",
                column: "campaign_recipient_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "registration",
                schema: "convocatorias");

            migrationBuilder.DropSequence(
                name: "registration_reference",
                schema: "convocatorias");
        }
    }
}
