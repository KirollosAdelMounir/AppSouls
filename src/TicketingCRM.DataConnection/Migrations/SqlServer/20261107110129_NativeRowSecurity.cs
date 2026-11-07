using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingCRM.DataConnection.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class NativeRowSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RowSecurityV1.SqlServerPrincipal);
            migrationBuilder.Sql(RowSecurityV1.SqlServerFunction);
            migrationBuilder.Sql(RowSecurityV1.SqlServerPolicies);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RowSecurityV1.SqlServerDown);

        }
    }
}
