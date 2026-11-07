using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingCRM.DataConnection.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class ChatAttachmentsAndTyping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupportChatMessage_TenantId_CustomerId_ChatId",
                table: "SupportChatMessage");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CustomerTypingUntilUtc",
                table: "SupportChat",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StaffTypingUntilUtc",
                table: "SupportChat",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_SupportChatMessage_TenantId_CustomerId_ChatId_Id",
                table: "SupportChatMessage",
                columns: new[] { "TenantId", "CustomerId", "ChatId", "Id" });

            migrationBuilder.CreateTable(
                name: "SupportChatAttachment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Filename = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportChatAttachment", x => x.Id);
                    table.UniqueConstraint("AK_SupportChatAttachment_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_SupportChatAttachment_SupportChatMessage_TenantId_CustomerI~",
                        columns: x => new { x.TenantId, x.CustomerId, x.ChatId, x.MessageId },
                        principalTable: "SupportChatMessage",
                        principalColumns: new[] { "TenantId", "CustomerId", "ChatId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportChatAttachment_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupportChatAttachment_TenantId_ChatId",
                table: "SupportChatAttachment",
                columns: new[] { "TenantId", "ChatId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportChatAttachment_TenantId_CustomerId",
                table: "SupportChatAttachment",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportChatAttachment_TenantId_CustomerId_ChatId_MessageId",
                table: "SupportChatAttachment",
                columns: new[] { "TenantId", "CustomerId", "ChatId", "MessageId" });
            migrationBuilder.Sql("""
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE "SupportChatAttachment" TO appsouls_runtime;
ALTER TABLE "SupportChatAttachment" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "SupportChatAttachment" FORCE ROW LEVEL SECURITY;
CREATE POLICY appsouls_scope_v1 ON "SupportChatAttachment" USING (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false)))) WITH CHECK (current_user <> 'appsouls_runtime' OR ("TenantId"::text=NULLIF(current_setting('appsouls.tenant_id',true),'') AND ((NULLIF(current_setting('appsouls.public_form_id',true),'') IS NULL AND (NULLIF(current_setting('appsouls.customer_id',true),'') IS NULL OR "CustomerId"::text=current_setting('appsouls.customer_id',true))) OR (false))));
ALTER TABLE "SupportChatAttachment" NO FORCE ROW LEVEL SECURITY;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.DropTable(
                name: "SupportChatAttachment");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_SupportChatMessage_TenantId_CustomerId_ChatId_Id",
                table: "SupportChatMessage");

            migrationBuilder.DropColumn(
                name: "CustomerTypingUntilUtc",
                table: "SupportChat");

            migrationBuilder.DropColumn(
                name: "StaffTypingUntilUtc",
                table: "SupportChat");

            migrationBuilder.CreateIndex(
                name: "IX_SupportChatMessage_TenantId_CustomerId_ChatId",
                table: "SupportChatMessage",
                columns: new[] { "TenantId", "CustomerId", "ChatId" });
        }
    }
}
