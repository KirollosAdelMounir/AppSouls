using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingCRM.DataConnection.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class CustomerDeliveryOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerInvitations_SalesOpportunities_TenantId_Opportunity~",
                table: "CustomerInvitations");

            migrationBuilder.DropIndex(
                name: "IX_SalesOpportunities_TenantId_CustomerId",
                table: "SalesOpportunities");

            migrationBuilder.DropIndex(
                name: "IX_CustomerInvitations_TenantId_OpportunityId",
                table: "CustomerInvitations");

            migrationBuilder.AddColumn<Guid>(
                name: "PublishedByUserId",
                table: "ProductReleases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RevokedAtUtc",
                table: "ProductReleases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RevokedByUserId",
                table: "ProductReleases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_SalesOpportunities_TenantId_CustomerId_Id",
                table: "SalesOpportunities",
                columns: new[] { "TenantId", "CustomerId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductReleases_TenantId_PublishedByUserId",
                table: "ProductReleases",
                columns: new[] { "TenantId", "PublishedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductReleases_TenantId_RevokedByUserId",
                table: "ProductReleases",
                columns: new[] { "TenantId", "RevokedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInvitations_TenantId_CustomerId_OpportunityId",
                table: "CustomerInvitations",
                columns: new[] { "TenantId", "CustomerId", "OpportunityId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerInvitations_SalesOpportunities_TenantId_CustomerId_~",
                table: "CustomerInvitations",
                columns: new[] { "TenantId", "CustomerId", "OpportunityId" },
                principalTable: "SalesOpportunities",
                principalColumns: new[] { "TenantId", "CustomerId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerInvitations_TenantMemberships_TenantId_UserId",
                table: "CustomerInvitations",
                columns: new[] { "TenantId", "UserId" },
                principalTable: "TenantMemberships",
                principalColumns: new[] { "TenantId", "UserId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductReleases_TenantMemberships_TenantId_PublishedByUserId",
                table: "ProductReleases",
                columns: new[] { "TenantId", "PublishedByUserId" },
                principalTable: "TenantMemberships",
                principalColumns: new[] { "TenantId", "UserId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductReleases_TenantMemberships_TenantId_RevokedByUserId",
                table: "ProductReleases",
                columns: new[] { "TenantId", "RevokedByUserId" },
                principalTable: "TenantMemberships",
                principalColumns: new[] { "TenantId", "UserId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerInvitations_SalesOpportunities_TenantId_CustomerId_~",
                table: "CustomerInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerInvitations_TenantMemberships_TenantId_UserId",
                table: "CustomerInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductReleases_TenantMemberships_TenantId_PublishedByUserId",
                table: "ProductReleases");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductReleases_TenantMemberships_TenantId_RevokedByUserId",
                table: "ProductReleases");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_SalesOpportunities_TenantId_CustomerId_Id",
                table: "SalesOpportunities");

            migrationBuilder.DropIndex(
                name: "IX_ProductReleases_TenantId_PublishedByUserId",
                table: "ProductReleases");

            migrationBuilder.DropIndex(
                name: "IX_ProductReleases_TenantId_RevokedByUserId",
                table: "ProductReleases");

            migrationBuilder.DropIndex(
                name: "IX_CustomerInvitations_TenantId_CustomerId_OpportunityId",
                table: "CustomerInvitations");

            migrationBuilder.DropColumn(
                name: "PublishedByUserId",
                table: "ProductReleases");

            migrationBuilder.DropColumn(
                name: "RevokedAtUtc",
                table: "ProductReleases");

            migrationBuilder.DropColumn(
                name: "RevokedByUserId",
                table: "ProductReleases");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOpportunities_TenantId_CustomerId",
                table: "SalesOpportunities",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInvitations_TenantId_OpportunityId",
                table: "CustomerInvitations",
                columns: new[] { "TenantId", "OpportunityId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerInvitations_SalesOpportunities_TenantId_Opportunity~",
                table: "CustomerInvitations",
                columns: new[] { "TenantId", "OpportunityId" },
                principalTable: "SalesOpportunities",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
