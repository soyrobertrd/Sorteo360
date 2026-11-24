using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendeeReinstatementAndDropSupplierQuoteTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_candidate_quote",
                schema: "events",
                table: "candidate");

            migrationBuilder.DropForeignKey(
                name: "fk_candidate_proposal_quote",
                schema: "events",
                table: "candidate_proposal");

            migrationBuilder.DropForeignKey(
                name: "fk_candidate_service_supplier",
                schema: "events",
                table: "candidate");

            migrationBuilder.DropForeignKey(
                name: "fk_candidate_proposal_candidate",
                schema: "events",
                table: "candidate_proposal");

            migrationBuilder.DropForeignKey(
                name: "fk_candidate_proposal_final_amount_quote_attachment",
                schema: "events",
                table: "candidate_proposal");

            migrationBuilder.DropForeignKey(
                name: "fk_candidate_proposal_pending_final_amount_quote_attachment",
                schema: "events",
                table: "candidate_proposal");

            migrationBuilder.DropTable(
                name: "picked_service",
                schema: "events");

            migrationBuilder.DropTable(
                name: "quote_decision",
                schema: "events");

            migrationBuilder.DropTable(
                name: "service_catalogue_item",
                schema: "events");

            migrationBuilder.DropTable(
                name: "service_supplier_evaluation",
                schema: "events");

            migrationBuilder.DropTable(
                name: "service_supplier_evaluation_reset",
                schema: "events");

            migrationBuilder.DropTable(
                name: "service_supplier_photo",
                schema: "events");

            migrationBuilder.DropTable(
                name: "signature_request_file",
                schema: "events");

            migrationBuilder.DropTable(
                name: "signature_request_signer",
                schema: "events");

            migrationBuilder.DropTable(
                name: "signature_request",
                schema: "events");

            migrationBuilder.DropTable(
                name: "quote",
                schema: "events");

            migrationBuilder.DropTable(
                name: "service_supplier",
                schema: "events");

            migrationBuilder.DropTable(
                name: "candidate",
                schema: "events");

            migrationBuilder.DropTable(
                name: "proposal_attachment",
                schema: "events");

            migrationBuilder.DropTable(
                name: "candidate_proposal",
                schema: "events");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reinstated_at",
                schema: "events",
                table: "raffle_attendee",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reinstated_by_id",
                schema: "events",
                table: "raffle_attendee",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reinstated_at",
                schema: "events",
                table: "raffle_attendee");

            migrationBuilder.DropColumn(
                name: "reinstated_by_id",
                schema: "events",
                table: "raffle_attendee");

            migrationBuilder.CreateTable(
                name: "quote",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "draft"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote", x => x.id);
                    table.CheckConstraint("ck_quote_category", "category IN ('Venue', 'Ambientacion', 'Audiovisuales', 'Seguridad', 'Traductores', 'Edecanes', 'Alimentacion', 'Transporte', 'Fotografia', 'Produccion', 'Tecnologia', 'Mobiliario', 'Entretenimiento', 'Limpieza', 'Logistica')");
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
                name: "service_supplier",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    auditorium_capacity = table.Column<int>(type: "integer", nullable: true),
                    category = table.Column<string>(type: "text", nullable: false),
                    cocktail_capacity = table.Column<int>(type: "integer", nullable: true),
                    comments = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_hireable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    secondary_categories = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'::text[]"),
                    unhireable_reason = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_supplier", x => x.id);
                    table.CheckConstraint("ck_service_supplier_capacity_category", "category = 'Venue' OR 'Venue' = ANY (secondary_categories) OR (auditorium_capacity IS NULL AND cocktail_capacity IS NULL)");
                    table.CheckConstraint("ck_service_supplier_capacity_positive", "(auditorium_capacity IS NULL OR auditorium_capacity > 0) AND (cocktail_capacity IS NULL OR cocktail_capacity > 0)");
                    table.CheckConstraint("ck_service_supplier_category", "category IN ('Venue', 'Ambientacion', 'Audiovisuales', 'Seguridad', 'Traductores', 'Edecanes', 'Alimentacion', 'Transporte', 'Fotografia', 'Produccion', 'Tecnologia', 'Mobiliario', 'Entretenimiento', 'Limpieza', 'Logistica')");
                    table.CheckConstraint("ck_service_supplier_secondary_categories", "secondary_categories <@ ARRAY['Venue', 'Ambientacion', 'Audiovisuales', 'Seguridad', 'Traductores', 'Edecanes', 'Alimentacion', 'Transporte', 'Fotografia', 'Produccion', 'Tecnologia', 'Mobiliario', 'Entretenimiento', 'Limpieza', 'Logistica']::text[] AND NOT (category = ANY (secondary_categories))");
                    table.CheckConstraint("ck_service_supplier_unhireable_reason", "is_hireable = false OR unhireable_reason IS NULL");
                });

            migrationBuilder.CreateTable(
                name: "signature_request",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: false),
                    failure_code = table.Column<string>(type: "text", nullable: true),
                    is_sequential = table.Column<bool>(type: "boolean", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    public_access_id = table.Column<string>(type: "text", nullable: true),
                    queued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reference = table.Column<string>(type: "text", nullable: false),
                    send_attempts = table.Column<int>(type: "integer", nullable: false),
                    send_may_have_landed = table.Column<bool>(type: "boolean", nullable: false),
                    sender_cedula = table.Column<string>(type: "text", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    state = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signature_request", x => x.id);
                    table.CheckConstraint("ck_signature_request_send_attempts", "send_attempts >= 0");
                    table.CheckConstraint("ck_signature_request_state", "state IN ('Queued', 'Sent', 'Completing', 'Signed', 'NotSent', 'SendUncertain', 'DownloadLost', 'Rejected', 'Expired')");
                });

            migrationBuilder.CreateTable(
                name: "quote_decision",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt = table.Column<int>(type: "integer", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    exception_justification = table.Column<string>(type: "text", nullable: true),
                    exception_reason = table.Column<string>(type: "text", nullable: true),
                    outcome = table.Column<string>(type: "text", nullable: true),
                    outcome_notification_id = table.Column<string>(type: "text", nullable: true),
                    outcome_notification_status = table.Column<string>(type: "text", nullable: true),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    submit_notification_id = table.Column<string>(type: "text", nullable: true),
                    submit_notification_status = table.Column<string>(type: "text", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    winner_proposal_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_decision", x => x.id);
                    table.CheckConstraint("ck_quote_decision_exception", "exception_reason IS NULL OR exception_reason IN ('single_provider', 'single_provider_date', 'otro')");
                    table.CheckConstraint("ck_quote_decision_outcome", "outcome IS NULL OR outcome IN ('approved', 'rejected', 'withdrawn', 'discarded')");
                    table.CheckConstraint("ck_quote_decision_outcome_notification_status", "outcome_notification_status IS NULL OR outcome_notification_status IN ('sent', 'no_recipient', 'failed')");
                    table.CheckConstraint("ck_quote_decision_submit_notification_status", "submit_notification_status IS NULL OR submit_notification_status IN ('sent', 'no_recipient', 'failed')");
                    table.ForeignKey(
                        name: "fk_quote_decision_quote",
                        column: x => x.quote_id,
                        principalSchema: "events",
                        principalTable: "quote",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "candidate",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    payment_bank_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate", x => x.id);
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
                name: "service_catalogue_item",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    service_supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit = table.Column<string>(type: "text", nullable: false),
                    reference_price_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    reference_price_currency = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_catalogue_item", x => x.id);
                    table.CheckConstraint("ck_service_catalogue_item_reference_price_currency", "reference_price_currency IN ('USD', 'EUR')");
                    table.CheckConstraint("ck_service_catalogue_item_reference_price_positive", "reference_price_amount > 0");
                    table.ForeignKey(
                        name: "fk_service_catalogue_item_service_supplier",
                        column: x => x.service_supplier_id,
                        principalSchema: "events",
                        principalTable: "service_supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_supplier_evaluation",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    communication = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    punctuality = table.Column<int>(type: "integer", nullable: false),
                    service_quality = table.Column<int>(type: "integer", nullable: false),
                    service_supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value_for_money = table.Column<int>(type: "integer", nullable: false),
                    would_hire_again = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_supplier_evaluation", x => x.id);
                    table.CheckConstraint("ck_service_supplier_evaluation_scores", "service_quality BETWEEN 1 AND 5 AND punctuality BETWEEN 1 AND 5 AND value_for_money BETWEEN 1 AND 5 AND communication BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_service_supplier_evaluation_event",
                        column: x => x.event_id,
                        principalSchema: "events",
                        principalTable: "event",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_supplier_evaluation_service_supplier",
                        column: x => x.service_supplier_id,
                        principalSchema: "events",
                        principalTable: "service_supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_supplier_evaluation_reset",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    reset_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reset_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_supplier_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_supplier_evaluation_reset", x => x.id);
                    table.CheckConstraint("ck_service_supplier_evaluation_reset_reason", "btrim(reason) <> ''");
                    table.ForeignKey(
                        name: "fk_service_supplier_evaluation_reset_service_supplier",
                        column: x => x.service_supplier_id,
                        principalSchema: "events",
                        principalTable: "service_supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_supplier_photo",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    service_supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stored_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_supplier_photo", x => x.id);
                    table.ForeignKey(
                        name: "fk_service_supplier_photo_service_supplier",
                        column: x => x.service_supplier_id,
                        principalSchema: "events",
                        principalTable: "service_supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_supplier_photo_stored_file",
                        column: x => x.stored_file_id,
                        principalSchema: "events",
                        principalTable: "stored_file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "signature_request_file",
                schema: "events",
                columns: table => new
                {
                    signature_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    stored_file_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signature_request_file", x => new { x.signature_request_id, x.role, x.position });
                    table.CheckConstraint("ck_signature_request_file_position", "position >= 0");
                    table.CheckConstraint("ck_signature_request_file_role", "role IN ('Sent', 'Signed')");
                    table.ForeignKey(
                        name: "fk_signature_request_file_signature_request",
                        column: x => x.signature_request_id,
                        principalSchema: "events",
                        principalTable: "signature_request",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_signature_request_file_stored_file",
                        column: x => x.stored_file_id,
                        principalSchema: "events",
                        principalTable: "stored_file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "signature_request_signer",
                schema: "events",
                columns: table => new
                {
                    signature_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    cedula = table.Column<string>(type: "text", nullable: false),
                    signing_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signature_request_signer", x => new { x.signature_request_id, x.position });
                    table.CheckConstraint("ck_signature_request_signer_action", "action IN ('Sign', 'Approval')");
                    table.CheckConstraint("ck_signature_request_signer_position", "position >= 0");
                    table.ForeignKey(
                        name: "fk_signature_request_signer_signature_request",
                        column: x => x.signature_request_id,
                        principalSchema: "events",
                        principalTable: "signature_request",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "candidate_proposal",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    final_amount_quote_attachment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_recommended = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_text = table.Column<string>(type: "text", nullable: true),
                    setup_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    teardown_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estimated_amount_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    estimated_amount_currency = table.Column<string>(type: "text", nullable: false),
                    final_amount_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    final_amount_currency = table.Column<string>(type: "text", nullable: true),
                    pending_final_amount_quote_attachment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pending_final_amount_recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    pending_final_amount_recorded_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pending_final_amount_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    pending_final_amount_currency = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_proposal", x => x.id);
                    table.CheckConstraint("ck_candidate_proposal_estimated_amount_currency", "estimated_amount_currency IN ('USD', 'EUR')");
                    table.CheckConstraint("ck_candidate_proposal_final_amount", "final_amount_amount IS NULL OR final_amount_currency IS NOT NULL");
                    table.CheckConstraint("ck_candidate_proposal_final_amount_currency", "final_amount_currency IS NULL OR final_amount_currency IN ('USD', 'EUR')");
                    table.CheckConstraint("ck_candidate_proposal_final_amount_quote", "final_amount_quote_attachment_id IS NULL OR final_amount_amount IS NOT NULL");
                    table.CheckConstraint("ck_candidate_proposal_pending_final_amount", "(pending_final_amount_amount IS NULL AND pending_final_amount_currency IS NULL AND pending_final_amount_recorded_by_id IS NULL AND pending_final_amount_recorded_at IS NULL) OR (pending_final_amount_amount IS NOT NULL AND pending_final_amount_currency IS NOT NULL AND pending_final_amount_recorded_by_id IS NOT NULL AND pending_final_amount_recorded_at IS NOT NULL)");
                    table.CheckConstraint("ck_candidate_proposal_pending_final_amount_currency", "pending_final_amount_currency IS NULL OR pending_final_amount_currency IN ('USD', 'EUR')");
                    table.CheckConstraint("ck_candidate_proposal_pending_final_amount_quote", "pending_final_amount_quote_attachment_id IS NULL OR pending_final_amount_amount IS NOT NULL");
                    table.CheckConstraint("ck_candidate_proposal_setup_before_teardown", "setup_date IS NULL OR teardown_date IS NULL OR teardown_date >= setup_date");
                    table.ForeignKey(
                        name: "fk_candidate_proposal_candidate",
                        column: x => x.candidate_id,
                        principalSchema: "events",
                        principalTable: "candidate",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_candidate_proposal_quote",
                        column: x => x.quote_id,
                        principalSchema: "events",
                        principalTable: "quote",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "picked_service",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_proposal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    service_catalogue_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    discount_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    discount_currency = table.Column<string>(type: "text", nullable: true),
                    tax_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    tax_currency = table.Column<string>(type: "text", nullable: true),
                    unit_price_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit_price_currency = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_picked_service", x => x.id);
                    table.CheckConstraint("ck_picked_service_discount", "discount_amount IS NULL OR (discount_amount >= 0 AND discount_currency IS NOT NULL)");
                    table.CheckConstraint("ck_picked_service_discount_currency", "discount_currency IS NULL OR discount_currency IN ('USD', 'EUR')");
                    table.CheckConstraint("ck_picked_service_quantity_positive", "quantity > 0");
                    table.CheckConstraint("ck_picked_service_tax", "tax_amount IS NULL OR (tax_amount >= 0 AND tax_currency IS NOT NULL)");
                    table.CheckConstraint("ck_picked_service_tax_currency", "tax_currency IS NULL OR tax_currency IN ('USD', 'EUR')");
                    table.CheckConstraint("ck_picked_service_unit_price_currency", "unit_price_currency IN ('USD', 'EUR')");
                    table.CheckConstraint("ck_picked_service_unit_price_positive", "unit_price_amount > 0");
                    table.ForeignKey(
                        name: "fk_picked_service_candidate_proposal",
                        column: x => x.candidate_proposal_id,
                        principalSchema: "events",
                        principalTable: "candidate_proposal",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "proposal_attachment",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_proposal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false, defaultValue: "Quote"),
                    stored_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proposal_attachment", x => x.id);
                    table.ForeignKey(
                        name: "fk_proposal_attachment_candidate_proposal",
                        column: x => x.candidate_proposal_id,
                        principalSchema: "events",
                        principalTable: "candidate_proposal",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_proposal_attachment_stored_file",
                        column: x => x.stored_file_id,
                        principalSchema: "events",
                        principalTable: "stored_file",
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
                name: "ux_candidate_quote_service_supplier",
                schema: "events",
                table: "candidate",
                columns: new[] { "quote_id", "service_supplier_id" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_proposal_candidate_id",
                schema: "events",
                table: "candidate_proposal",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_proposal_final_amount_quote_attachment_id",
                schema: "events",
                table: "candidate_proposal",
                column: "final_amount_quote_attachment_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_proposal_pending_final_amount_quote_attachment_id",
                schema: "events",
                table: "candidate_proposal",
                column: "pending_final_amount_quote_attachment_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_proposal_quote_id",
                schema: "events",
                table: "candidate_proposal",
                column: "quote_id");

            migrationBuilder.CreateIndex(
                name: "ux_candidate_proposal_final_amount",
                schema: "events",
                table: "candidate_proposal",
                column: "quote_id",
                unique: true,
                filter: "final_amount_amount IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_candidate_proposal_recommended",
                schema: "events",
                table: "candidate_proposal",
                column: "quote_id",
                unique: true,
                filter: "is_recommended AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_picked_service_candidate_proposal_id",
                schema: "events",
                table: "picked_service",
                column: "candidate_proposal_id");

            migrationBuilder.CreateIndex(
                name: "ix_proposal_attachment_candidate_proposal_id",
                schema: "events",
                table: "proposal_attachment",
                column: "candidate_proposal_id");

            migrationBuilder.CreateIndex(
                name: "ix_proposal_attachment_stored_file_id",
                schema: "events",
                table: "proposal_attachment",
                column: "stored_file_id");

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
                name: "ix_service_catalogue_item_service_supplier_id",
                schema: "events",
                table: "service_catalogue_item",
                column: "service_supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_supplier_active",
                schema: "events",
                table: "service_supplier",
                column: "category",
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_service_supplier_company_category",
                schema: "events",
                table: "service_supplier",
                columns: new[] { "company_id", "category" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_service_supplier_evaluation_event_id",
                schema: "events",
                table: "service_supplier_evaluation",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ux_service_supplier_evaluation_service_supplier_event",
                schema: "events",
                table: "service_supplier_evaluation",
                columns: new[] { "service_supplier_id", "event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_supplier_evaluation_reset_service_supplier_reset_at",
                schema: "events",
                table: "service_supplier_evaluation_reset",
                columns: new[] { "service_supplier_id", "reset_at" });

            migrationBuilder.CreateIndex(
                name: "ix_service_supplier_photo_service_supplier_id",
                schema: "events",
                table: "service_supplier_photo",
                column: "service_supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_supplier_photo_stored_file_id",
                schema: "events",
                table: "service_supplier_photo",
                column: "stored_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_signature_request_due",
                schema: "events",
                table: "signature_request",
                column: "next_attempt_at",
                filter: "state IN ('Queued', 'Completing')");

            migrationBuilder.CreateIndex(
                name: "ix_signature_request_file_stored_file_id",
                schema: "events",
                table: "signature_request_file",
                column: "stored_file_id");

            migrationBuilder.AddForeignKey(
                name: "fk_candidate_proposal_final_amount_quote_attachment",
                schema: "events",
                table: "candidate_proposal",
                column: "final_amount_quote_attachment_id",
                principalSchema: "events",
                principalTable: "proposal_attachment",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_candidate_proposal_pending_final_amount_quote_attachment",
                schema: "events",
                table: "candidate_proposal",
                column: "pending_final_amount_quote_attachment_id",
                principalSchema: "events",
                principalTable: "proposal_attachment",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
