using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingCRM.DataConnection.Migrations.SqlServer
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
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StaffTypingUntilUtc",
                table: "SupportChat",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_SupportChatMessage_TenantId_CustomerId_ChatId_Id",
                table: "SupportChatMessage",
                columns: new[] { "TenantId", "CustomerId", "ChatId", "Id" });

            migrationBuilder.CreateTable(
                name: "SupportChatAttachment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChatId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Filename = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportChatAttachment", x => x.Id);
                    table.UniqueConstraint("AK_SupportChatAttachment_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_SupportChatAttachment_SupportChatMessage_TenantId_CustomerId_ChatId_MessageId",
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
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.[SupportChatAttachment] TO [appsouls_runtime];
CREATE SECURITY POLICY dbo.[AppSouls_SupportChatAttachment_V1] ADD FILTER PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[SupportChatAttachment], ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[SupportChatAttachment] AFTER INSERT, ADD BLOCK PREDICATE dbo.AppSoulsRowScopeV1([TenantId],[CustomerId],NULL,0,0) ON dbo.[SupportChatAttachment] AFTER UPDATE WITH (STATE=ON,SCHEMABINDING=ON);
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SECURITY POLICY dbo.[AppSouls_SupportChatAttachment_V1];");

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
