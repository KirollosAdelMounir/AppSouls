using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingCRM.DataConnection.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class InstallationLicensesSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LicenseRevisions_TenantId_EntitlementId_Revision",
                table: "LicenseRevisions");

            migrationBuilder.AddColumn<Guid>(
                name: "InstallationId",
                table: "LicenseRevisions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "EnrollmentCounter",
                table: "Entitlements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Installations_TenantId_CustomerId_ProductId_Id",
                table: "Installations",
                columns: new[] { "TenantId", "CustomerId", "ProductId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_LicenseRevisions_TenantId_CustomerId_ProductId_InstallationId",
                table: "LicenseRevisions",
                columns: new[] { "TenantId", "CustomerId", "ProductId", "InstallationId" });

            migrationBuilder.CreateIndex(
                name: "IX_LicenseRevisions_TenantId_EntitlementId",
                table: "LicenseRevisions",
                columns: new[] { "TenantId", "EntitlementId" });

            migrationBuilder.CreateIndex(
                name: "IX_LicenseRevisions_TenantId_InstallationId_Revision",
                table: "LicenseRevisions",
                columns: new[] { "TenantId", "InstallationId", "Revision" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LicenseRevisions_Installations_TenantId_CustomerId_ProductId_InstallationId",
                table: "LicenseRevisions",
                columns: new[] { "TenantId", "CustomerId", "ProductId", "InstallationId" },
                principalTable: "Installations",
                principalColumns: new[] { "TenantId", "CustomerId", "ProductId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LicenseRevisions_Installations_TenantId_CustomerId_ProductId_InstallationId",
                table: "LicenseRevisions");

            migrationBuilder.DropIndex(
                name: "IX_LicenseRevisions_TenantId_CustomerId_ProductId_InstallationId",
                table: "LicenseRevisions");

            migrationBuilder.DropIndex(
                name: "IX_LicenseRevisions_TenantId_EntitlementId",
                table: "LicenseRevisions");

            migrationBuilder.DropIndex(
                name: "IX_LicenseRevisions_TenantId_InstallationId_Revision",
                table: "LicenseRevisions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Installations_TenantId_CustomerId_ProductId_Id",
                table: "Installations");

            migrationBuilder.DropColumn(
                name: "InstallationId",
                table: "LicenseRevisions");

            migrationBuilder.DropColumn(
                name: "EnrollmentCounter",
                table: "Entitlements");

            migrationBuilder.CreateIndex(
                name: "IX_LicenseRevisions_TenantId_EntitlementId_Revision",
                table: "LicenseRevisions",
                columns: new[] { "TenantId", "EntitlementId", "Revision" },
                unique: true);
        }
    }
}
