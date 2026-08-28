using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShipmentTelemetry.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "downstream_milestone_notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IntegrationEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<string>(type: "text", nullable: false),
                    Milestone = table.Column<string>(type: "text", nullable: false),
                    NotifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_downstream_milestone_notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "processed_integration_messages",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageType = table.Column<string>(type: "text", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_integration_messages", x => x.MessageId);
                });

            migrationBuilder.CreateTable(
                name: "processed_telemetry",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContainerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ShipmentId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_telemetry", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "quarantined_telemetry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContainerId = table.Column<string>(type: "text", nullable: false),
                    ShipmentId = table.Column<string>(type: "text", nullable: false),
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    QuarantinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quarantined_telemetry", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "shipment_operational_read_models",
                columns: table => new
                {
                    ShipmentId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContainerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CurrentMilestone = table.Column<int>(type: "integer", nullable: false),
                    LastAcceptedSequence = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_operational_read_models", x => x.ShipmentId);
                });

            migrationBuilder.CreateTable(
                name: "shipment_operational_states",
                columns: table => new
                {
                    ShipmentId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContainerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CurrentMilestone = table.Column<int>(type: "integer", nullable: false),
                    LastAcceptedSequence = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_operational_states", x => x.ShipmentId);
                });

            migrationBuilder.CreateTable(
                name: "telemetry_statuses",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    CurrentMilestone = table.Column<int>(type: "integer", nullable: true),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_statuses", x => x.EventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_downstream_milestone_notifications_IntegrationEventId",
                table: "downstream_milestone_notifications",
                column: "IntegrationEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_Status_CreatedAt",
                table: "outbox_messages",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_processed_integration_messages_MessageId",
                table: "processed_integration_messages",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_processed_telemetry_ContainerId_SequenceNumber",
                table: "processed_telemetry",
                columns: new[] { "ContainerId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_processed_telemetry_EventId",
                table: "processed_telemetry",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_processed_telemetry_ProcessedAt",
                table: "processed_telemetry",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_quarantined_telemetry_EventId",
                table: "quarantined_telemetry",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_operational_states_ContainerId",
                table: "shipment_operational_states",
                column: "ContainerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "downstream_milestone_notifications");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "processed_integration_messages");

            migrationBuilder.DropTable(
                name: "processed_telemetry");

            migrationBuilder.DropTable(
                name: "quarantined_telemetry");

            migrationBuilder.DropTable(
                name: "shipment_operational_read_models");

            migrationBuilder.DropTable(
                name: "shipment_operational_states");

            migrationBuilder.DropTable(
                name: "telemetry_statuses");
        }
    }
}
