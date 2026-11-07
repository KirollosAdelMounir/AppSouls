using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingCRM.DataConnection.Migrations.PostgreSql
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReadThroughUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
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
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "SupportChatReadState" TO appsouls_runtime;
ALTER TABLE "SupportChatReadState" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "SupportChatReadState" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "SupportChatReadState" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false))));
ALTER TABLE "SupportChatReadState" NO FORCE ROW LEVEL SECURITY;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.DropTable(
                name: "SupportChatReadState");
        }
    }
}
