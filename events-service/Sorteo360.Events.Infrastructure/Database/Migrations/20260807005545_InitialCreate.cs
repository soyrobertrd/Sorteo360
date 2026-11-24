using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "events");

            migrationBuilder.CreateTable(
                name: "event",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    opt_office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opt_contact_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "planning"),
                    language = table.Column<string>(type: "text", nullable: true),
                    external_url = table.Column<string>(type: "text", nullable: true),
                    invited_count = table.Column<int>(type: "integer", nullable: true),
                    total_cost_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    total_cost_currency = table.Column<string>(type: "text", nullable: true),
                    cancellation_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event", x => x.id);
                    table.CheckConstraint("ck_event_dates", "end_date >= start_date");
                    table.CheckConstraint("ck_event_status", "status IN ('planning', 'in_progress', 'closed', 'cancelled')");
                    table.CheckConstraint("ck_event_total_cost", "total_cost_amount IS NULL OR total_cost_currency IS NOT NULL");
                    table.CheckConstraint("ck_event_total_cost_currency", "total_cost_currency IS NULL OR total_cost_currency IN ('USD', 'EUR')");
                    table.CheckConstraint("ck_event_type", "type IN ('Roadshow', 'Feria', 'Tradeshow', 'Webinar')");
                });

            migrationBuilder.CreateTable(
                name: "service_supplier",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    company = table.Column<string>(type: "text", nullable: false),
                    contact_name = table.Column<string>(type: "text", nullable: false),
                    contact_last_name = table.Column<string>(type: "text", nullable: true),
                    contact_email = table.Column<string>(type: "text", nullable: false),
                    contact_phone = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    location_map_url = table.Column<string>(type: "text", nullable: true),
                    comments = table.Column<string>(type: "text", nullable: true),
                    country = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_supplier", x => x.id);
                    table.CheckConstraint("ck_service_supplier_category", "category IN ('Venue', 'Ambientacion', 'Audiovisuales', 'Seguridad', 'Traductores', 'Edecanes', 'Alimentacion', 'Otros')");
                });

            migrationBuilder.CreateTable(
                name: "quote",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "draft"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote", x => x.id);
                    table.CheckConstraint("ck_quote_category", "category IN ('Venue', 'Ambientacion', 'Audiovisuales', 'Seguridad', 'Traductores', 'Edecanes', 'Alimentacion', 'Otros')");
                    table.CheckConstraint("ck_quote_status", "status IN ('draft', 'submitted', 'approved')");
                    table.ForeignKey(
                        name: "fk_quote_event",
                        column: x => x.event_id,
                        principalSchema: "events",
                        principalTable: "event",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "roadshow_city",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    country = table.Column<string>(type: "text", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roadshow_city", x => x.id);
                    table.ForeignKey(
                        name: "fk_roadshow_city_event",
                        column: x => x.event_id,
                        principalSchema: "events",
                        principalTable: "event",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_supplier_photo",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_ref_id = table.Column<string>(type: "text", nullable: true),
                    url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_supplier_photo", x => x.id);
                    table.CheckConstraint("ck_service_supplier_photo_ref", "file_ref_id IS NOT NULL OR url IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_service_supplier_photo_service_supplier",
                        column: x => x.service_supplier_id,
                        principalSchema: "events",
                        principalTable: "service_supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "candidate",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_text = table.Column<string>(type: "text", nullable: true),
                    quote_photo_ref = table.Column<string>(type: "text", nullable: true),
                    estimated_amount_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    estimated_amount_currency = table.Column<string>(type: "text", nullable: false),
                    final_amount_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    final_amount_currency = table.Column<string>(type: "text", nullable: true),
                    is_recommended = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate", x => x.id);
                    table.CheckConstraint("ck_candidate_estimated_amount_currency", "estimated_amount_currency IN ('USD', 'EUR')");
                    table.CheckConstraint("ck_candidate_final_amount", "final_amount_amount IS NULL OR final_amount_currency IS NOT NULL");
                    table.CheckConstraint("ck_candidate_final_amount_currency", "final_amount_currency IS NULL OR final_amount_currency IN ('USD', 'EUR')");
                    table.ForeignKey(
                        name: "fk_candidate_quote",
                        column: x => x.quote_id,
                        principalSchema: "events",
                        principalTable: "quote",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_candidate_service_supplier",
                        column: x => x.service_supplier_id,
                        principalSchema: "events",
                        principalTable: "service_supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quote_decision",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt = table.Column<int>(type: "integer", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outcome = table.Column<string>(type: "text", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    winner_candidate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approval_ref_id = table.Column<string>(type: "text", nullable: true),
                    exception_reason = table.Column<string>(type: "text", nullable: true),
                    exception_justification = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_decision", x => x.id);
                    table.CheckConstraint("ck_quote_decision_exception", "exception_reason IS NULL OR exception_reason IN ('single_provider', 'single_provider_date', 'otro')");
                    table.CheckConstraint("ck_quote_decision_outcome", "outcome IS NULL OR outcome IN ('approved', 'rejected', 'withdrawn', 'discarded')");
                    table.ForeignKey(
                        name: "fk_quote_decision_quote",
                        column: x => x.quote_id,
                        principalSchema: "events",
                        principalTable: "quote",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_candidate_quote_id",
                schema: "events",
                table: "candidate",
                column: "quote_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_service_supplier_id",
                schema: "events",
                table: "candidate",
                column: "service_supplier_id");

            migrationBuilder.CreateIndex(
                name: "ux_candidate_final_amount",
                schema: "events",
                table: "candidate",
                column: "quote_id",
                unique: true,
                filter: "final_amount_amount IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_candidate_recommended",
                schema: "events",
                table: "candidate",
                column: "quote_id",
                unique: true,
                filter: "is_recommended AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_event_active",
                schema: "events",
                table: "event",
                columns: new[] { "type", "status" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_event_start_date",
                schema: "events",
                table: "event",
                column: "start_date",
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_quote_event_id",
                schema: "events",
                table: "quote",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ux_quote_event_category",
                schema: "events",
                table: "quote",
                columns: new[] { "event_id", "category" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_quote_decision_quote_id",
                schema: "events",
                table: "quote_decision",
                column: "quote_id");

            migrationBuilder.CreateIndex(
                name: "ux_quote_decision_attempt",
                schema: "events",
                table: "quote_decision",
                columns: new[] { "quote_id", "attempt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_quote_decision_pending",
                schema: "events",
                table: "quote_decision",
                column: "quote_id",
                unique: true,
                filter: "outcome IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_roadshow_city_event_id",
                schema: "events",
                table: "roadshow_city",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_supplier_active",
                schema: "events",
                table: "service_supplier",
                columns: new[] { "category", "country" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_service_supplier_photo_service_supplier_id",
                schema: "events",
                table: "service_supplier_photo",
                column: "service_supplier_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate",
                schema: "events");

            migrationBuilder.DropTable(
                name: "quote_decision",
                schema: "events");

            migrationBuilder.DropTable(
                name: "roadshow_city",
                schema: "events");

            migrationBuilder.DropTable(
                name: "service_supplier_photo",
                schema: "events");

            migrationBuilder.DropTable(
                name: "quote",
                schema: "events");

            migrationBuilder.DropTable(
                name: "service_supplier",
                schema: "events");

            migrationBuilder.DropTable(
                name: "event",
                schema: "events");
        }
    }
}
