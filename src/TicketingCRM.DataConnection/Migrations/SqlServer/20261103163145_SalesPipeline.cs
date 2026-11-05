using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingCRM.DataConnection.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class SalesPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_TenantMemberships_TenantId_UserId",
                table: "TenantMemberships",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateTable(
                name: "SalesLeads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FollowUpAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreationOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreationDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConversionOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConversionDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesLeads", x => x.Id);
                    table.UniqueConstraint("AK_SalesLeads_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_SalesLeads_Customers_TenantId_CustomerId",
                        columns: x => new { x.TenantId, x.CustomerId },
                        principalTable: "Customers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesLeads_TenantMemberships_TenantId_CreatedByUserId",
                        columns: x => new { x.TenantId, x.CreatedByUserId },
                        principalTable: "TenantMemberships",
                        principalColumns: new[] { "TenantId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesLeads_TenantMemberships_TenantId_OwnerUserId",
                        columns: x => new { x.TenantId, x.OwnerUserId },
                        principalTable: "TenantMemberships",
                        principalColumns: new[] { "TenantId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesLeads_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesOpportunities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceLeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpectedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    FollowUpAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreationOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreationDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOpportunities", x => x.Id);
                    table.UniqueConstraint("AK_SalesOpportunities_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_SalesOpportunities_Customers_TenantId_CustomerId",
                        columns: x => new { x.TenantId, x.CustomerId },
                        principalTable: "Customers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOpportunities_SalesLeads_TenantId_SourceLeadId",
                        columns: x => new { x.TenantId, x.SourceLeadId },
                        principalTable: "SalesLeads",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOpportunities_TenantMemberships_TenantId_CreatedByUserId",
                        columns: x => new { x.TenantId, x.CreatedByUserId },
                        principalTable: "TenantMemberships",
                        principalColumns: new[] { "TenantId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOpportunities_TenantMemberships_TenantId_OwnerUserId",
                        columns: x => new { x.TenantId, x.OwnerUserId },
                        principalTable: "TenantMemberships",
                        principalColumns: new[] { "TenantId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOpportunities_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FromStage = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ToStage = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesHistories", x => x.Id);
                    table.UniqueConstraint("AK_SalesHistories_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_SalesHistory_Subject", "([LeadId] IS NOT NULL AND [OpportunityId] IS NULL) OR ([LeadId] IS NULL AND [OpportunityId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_SalesHistories_SalesLeads_TenantId_LeadId",
                        columns: x => new { x.TenantId, x.LeadId },
                        principalTable: "SalesLeads",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesHistories_SalesOpportunities_TenantId_OpportunityId",
                        columns: x => new { x.TenantId, x.OpportunityId },
                        principalTable: "SalesOpportunities",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesHistories_TenantMemberships_TenantId_ActorUserId",
                        columns: x => new { x.TenantId, x.ActorUserId },
                        principalTable: "TenantMemberships",
                        principalColumns: new[] { "TenantId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesHistories_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesHistories_TenantId_ActorUserId_LeadId_OperationId",
                table: "SalesHistories",
                columns: new[] { "TenantId", "ActorUserId", "LeadId", "OperationId" },
                unique: true,
                filter: "[LeadId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SalesHistories_TenantId_ActorUserId_OpportunityId_OperationId",
                table: "SalesHistories",
                columns: new[] { "TenantId", "ActorUserId", "OpportunityId", "OperationId" },
                unique: true,
                filter: "[OpportunityId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SalesHistories_TenantId_LeadId",
                table: "SalesHistories",
                columns: new[] { "TenantId", "LeadId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesHistories_TenantId_OpportunityId",
                table: "SalesHistories",
                columns: new[] { "TenantId", "OpportunityId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesLeads_TenantId_CreatedAtUtc_Id",
                table: "SalesLeads",
                columns: new[] { "TenantId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesLeads_TenantId_CreatedByUserId_CreationOperationId",
                table: "SalesLeads",
                columns: new[] { "TenantId", "CreatedByUserId", "CreationOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesLeads_TenantId_CustomerId",
                table: "SalesLeads",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesLeads_TenantId_OwnerUserId",
                table: "SalesLeads",
                columns: new[] { "TenantId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOpportunities_TenantId_CreatedAtUtc_Id",
                table: "SalesOpportunities",
                columns: new[] { "TenantId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOpportunities_TenantId_CreatedByUserId_CreationOperationId",
                table: "SalesOpportunities",
                columns: new[] { "TenantId", "CreatedByUserId", "CreationOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOpportunities_TenantId_CustomerId",
                table: "SalesOpportunities",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOpportunities_TenantId_OwnerUserId",
                table: "SalesOpportunities",
                columns: new[] { "TenantId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOpportunities_TenantId_SourceLeadId",
                table: "SalesOpportunities",
                columns: new[] { "TenantId", "SourceLeadId" },
                unique: true,
                filter: "[SourceLeadId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesHistories");

            migrationBuilder.DropTable(
                name: "SalesOpportunities");

            migrationBuilder.DropTable(
                name: "SalesLeads");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TenantMemberships_TenantId_UserId",
                table: "TenantMemberships");
        }
    }
}
