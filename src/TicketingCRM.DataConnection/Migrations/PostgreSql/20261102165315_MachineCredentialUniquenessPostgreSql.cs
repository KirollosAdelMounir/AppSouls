using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingCRM.DataConnection.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class MachineCredentialUniquenessPostgreSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CredentialHash",
                table: "Installations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

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
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);
        }
    }
}
