using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingCRM.DataConnection.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class MachineCredentialUniquenessSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CredentialHash",
                table: "Installations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Installations_CredentialHash",
                table: "Installations",
                column: "CredentialHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Installations_CredentialHash",
                table: "Installations");

            migrationBuilder.AlterColumn<string>(
                name: "CredentialHash",
                table: "Installations",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);
        }
    }
}
