using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingCRM.DataConnection.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class SupportStageIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OperationId",
                table: "SupportTicketHistories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestDigest",
                table: "SupportTicketHistories",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportTicketHistories_TenantId_ActorUserId_OperationId",
                table: "SupportTicketHistories",
                columns: new[] { "TenantId", "ActorUserId", "OperationId" },
                unique: true,
                filter: "\"OperationId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupportTicketHistories_TenantId_ActorUserId_OperationId",
                table: "SupportTicketHistories");

            migrationBuilder.DropColumn(
                name: "OperationId",
                table: "SupportTicketHistories");

            migrationBuilder.DropColumn(
                name: "RequestDigest",
                table: "SupportTicketHistories");
        }
    }
}
