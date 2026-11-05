using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingCRM.DataConnection.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class PlatformAdministrator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPlatformAdministrator",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPlatformAdministrator",
                table: "AspNetUsers");
        }
    }
}
