using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Convocatorias.Infrastructure.Database.Migrations
{
    /// <summary>
    /// La tabla del stub de envío (spec <c>04</c> §4.1). Corre en <b>todos</b> los entornos y se
    /// queda vacía donde el transporte es <c>http</c>: una migración que ramificara por entorno
    /// rompería la regla de que las migraciones son la única autoridad del esquema, y lo haría
    /// para ahorrarse una tabla muerta.
    /// <para>
    /// <b>Es temporal con fecha de salida.</b> La migración que traiga el cliente real de
    /// notification-service la borra, y con ella se van la clase del stub y el ajuste
    /// <c>Notifications__Transport</c> (§4.2).
    /// </para>
    /// </summary>
    public partial class AddStubNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stub_notification",
                schema: "convocatorias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_email = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    language = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    body_html = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stub_notification", x => x.id);
                    table.CheckConstraint("ck_stub_notification_language", "language IN ('de', 'en', 'es', 'fr', 'it', 'pt')");
                    table.CheckConstraint("ck_stub_notification_reason_matches_status", "(status = 'rejected') = (failure_reason IS NOT NULL)");
                    table.CheckConstraint("ck_stub_notification_status", "status IN ('accepted', 'rejected')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_stub_notification_batch",
                schema: "convocatorias",
                table: "stub_notification",
                column: "batch_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stub_notification",
                schema: "convocatorias");
        }
    }
}
