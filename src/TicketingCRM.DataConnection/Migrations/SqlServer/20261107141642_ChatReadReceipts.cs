using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingCRM.DataConnection.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class ChatReadReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupportChatReadState",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChatId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReadThroughUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportChatReadState", x => x.Id);
                    table.UniqueConstraint("AK_SupportChatReadState_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_SupportChatReadState_SupportChat_TenantId_CustomerId_ChatId",
                        columns: x => new { x.TenantId, x.CustomerId, x.ChatId },
                        principalTable: "SupportChat",
                        principalColumns: new[] { "TenantId", "CustomerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportChatReadState_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupportChatReadState_TenantId_CustomerId_ChatId",
                table: "SupportChatReadState",
                columns: new[] { "TenantId", "CustomerId", "ChatId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportChatReadState_TenantId_UserId_ChatId",
                table: "SupportChatReadState",
                columns: new[] { "TenantId", "UserId", "ChatId" },
                unique: true);
            migrationBuilder.Sql("""
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[SupportChatReadState] TO [appsouls_runtime];
CREATE SECURITY POLICY dbo.[AppSouls_SupportChatReadState_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[SupportChatReadState], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[SupportChatReadState] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[SupportChatReadState] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SECURITY POLICY dbo.[AppSouls_SupportChatReadState_V1];");

            migrationBuilder.DropTable(
                name: "SupportChatReadState");
        }
    }
}
