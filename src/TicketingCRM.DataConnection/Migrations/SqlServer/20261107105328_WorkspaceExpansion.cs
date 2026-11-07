using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingCRM.DataConnection.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class WorkspaceExpansion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupportTickets_TenantId_CustomerId",
                table: "SupportTickets");

            migrationBuilder.AddColumn<string>(
                name: "Urgency",
                table: "SupportTickets",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Medium");

            migrationBuilder.AddColumn<string>(
                name: "StoreUrl",
                table: "ProductReleases",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SupportExpiresAtUtc",
                table: "Entitlements",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_SupportTickets_TenantId_CustomerId_Id",
                table: "SupportTickets",
                columns: new[] { "TenantId", "CustomerId", "Id" });

            migrationBuilder.CreateTable(
                name: "CommerceOrder",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LinesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 64000, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommerceOrder", x => x.Id);
                    table.UniqueConstraint("AK_CommerceOrder_TenantId_CustomerId_Id", x => new { x.TenantId, x.CustomerId, x.Id });
                    table.UniqueConstraint("AK_CommerceOrder_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_CommerceOrder_Contracts_TenantId_CustomerId_ContractId",
                        columns: x => new { x.TenantId, x.CustomerId, x.ContractId },
                        principalTable: "Contracts",
                        principalColumns: new[] { "TenantId", "CustomerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommerceOrder_Customers_TenantId_CustomerId",
                        columns: x => new { x.TenantId, x.CustomerId },
                        principalTable: "Customers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommerceOrder_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductPricing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPricing", x => x.Id);
                    table.UniqueConstraint("AK_ProductPricing_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_ProductPricing_Products_TenantId_ProductId",
                        columns: x => new { x.TenantId, x.ProductId },
                        principalTable: "Products",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductPricing_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupportChat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Topic = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportChat", x => x.Id);
                    table.UniqueConstraint("AK_SupportChat_TenantId_CustomerId_Id", x => new { x.TenantId, x.CustomerId, x.Id });
                    table.UniqueConstraint("AK_SupportChat_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_SupportChat_Customers_TenantId_CustomerId",
                        columns: x => new { x.TenantId, x.CustomerId },
                        principalTable: "Customers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportChat_Products_TenantId_ProductId",
                        columns: x => new { x.TenantId, x.ProductId },
                        principalTable: "Products",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportChat_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupportReply",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AuthorRole = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportReply", x => x.Id);
                    table.UniqueConstraint("AK_SupportReply_TenantId_CustomerId_TicketId_Id", x => new { x.TenantId, x.CustomerId, x.TicketId, x.Id });
                    table.UniqueConstraint("AK_SupportReply_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_SupportReply_SupportTickets_TenantId_CustomerId_TicketId",
                        columns: x => new { x.TenantId, x.CustomerId, x.TicketId },
                        principalTable: "SupportTickets",
                        principalColumns: new[] { "TenantId", "CustomerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportReply_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceDocument",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 128000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceDocument", x => x.Id);
                    table.UniqueConstraint("AK_InvoiceDocument_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_InvoiceDocument_CommerceOrder_TenantId_CustomerId_OrderId",
                        columns: x => new { x.TenantId, x.CustomerId, x.OrderId },
                        principalTable: "CommerceOrder",
                        principalColumns: new[] { "TenantId", "CustomerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceDocument_Contracts_TenantId_CustomerId_ContractId",
                        columns: x => new { x.TenantId, x.CustomerId, x.ContractId },
                        principalTable: "Contracts",
                        principalColumns: new[] { "TenantId", "CustomerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceDocument_Customers_TenantId_CustomerId",
                        columns: x => new { x.TenantId, x.CustomerId },
                        principalTable: "Customers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceDocument_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupportChatMessage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChatId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AuthorRole = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportChatMessage", x => x.Id);
                    table.UniqueConstraint("AK_SupportChatMessage_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_SupportChatMessage_SupportChat_TenantId_CustomerId_ChatId",
                        columns: x => new { x.TenantId, x.CustomerId, x.ChatId },
                        principalTable: "SupportChat",
                        principalColumns: new[] { "TenantId", "CustomerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportChatMessage_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupportAttachment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReplyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_SupportAttachment", x => x.Id);
                    table.UniqueConstraint("AK_SupportAttachment_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_SupportAttachment_SupportReply_TenantId_CustomerId_TicketId_ReplyId",
                        columns: x => new { x.TenantId, x.CustomerId, x.TicketId, x.ReplyId },
                        principalTable: "SupportReply",
                        principalColumns: new[] { "TenantId", "CustomerId", "TicketId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportAttachment_SupportTickets_TenantId_CustomerId_TicketId",
                        columns: x => new { x.TenantId, x.CustomerId, x.TicketId },
                        principalTable: "SupportTickets",
                        principalColumns: new[] { "TenantId", "CustomerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportAttachment_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_TenantId_CustomerId_CreatedAtUtc",
                table: "SupportTickets",
                columns: new[] { "TenantId", "CustomerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductReleases_TenantId_Status_PublishedAtUtc",
                table: "ProductReleases",
                columns: new[] { "TenantId", "Status", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Entitlements_TenantId_CustomerId_ProductId_ExpiresAtUtc",
                table: "Entitlements",
                columns: new[] { "TenantId", "CustomerId", "ProductId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_TenantId_CustomerId_StartsAtUtc",
                table: "Contracts",
                columns: new[] { "TenantId", "CustomerId", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CommerceOrder_TenantId_CustomerId_ContractId",
                table: "CommerceOrder",
                columns: new[] { "TenantId", "CustomerId", "ContractId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommerceOrder_TenantId_CustomerId_CreatedAtUtc",
                table: "CommerceOrder",
                columns: new[] { "TenantId", "CustomerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CommerceOrder_TenantId_Reference",
                table: "CommerceOrder",
                columns: new[] { "TenantId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDocument_TenantId_CustomerId_ContractId",
                table: "InvoiceDocument",
                columns: new[] { "TenantId", "CustomerId", "ContractId" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDocument_TenantId_CustomerId_CreatedAtUtc",
                table: "InvoiceDocument",
                columns: new[] { "TenantId", "CustomerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDocument_TenantId_CustomerId_OrderId",
                table: "InvoiceDocument",
                columns: new[] { "TenantId", "CustomerId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDocument_TenantId_Number",
                table: "InvoiceDocument",
                columns: new[] { "TenantId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductPricing_TenantId_ProductId",
                table: "ProductPricing",
                columns: new[] { "TenantId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportAttachment_TenantId_CustomerId",
                table: "SupportAttachment",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportAttachment_TenantId_CustomerId_TicketId_ReplyId",
                table: "SupportAttachment",
                columns: new[] { "TenantId", "CustomerId", "TicketId", "ReplyId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportAttachment_TenantId_TicketId",
                table: "SupportAttachment",
                columns: new[] { "TenantId", "TicketId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportChat_TenantId_CustomerId_CreatedAtUtc",
                table: "SupportChat",
                columns: new[] { "TenantId", "CustomerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportChat_TenantId_ProductId",
                table: "SupportChat",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportChat_TenantId_Topic_Status_CreatedAtUtc",
                table: "SupportChat",
                columns: new[] { "TenantId", "Topic", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportChatMessage_TenantId_AuthorUserId_CreatedAtUtc",
                table: "SupportChatMessage",
                columns: new[] { "TenantId", "AuthorUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportChatMessage_TenantId_ChatId_CreatedAtUtc",
                table: "SupportChatMessage",
                columns: new[] { "TenantId", "ChatId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportChatMessage_TenantId_CustomerId_ChatId",
                table: "SupportChatMessage",
                columns: new[] { "TenantId", "CustomerId", "ChatId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportReply_TenantId_AuthorUserId_CreatedAtUtc",
                table: "SupportReply",
                columns: new[] { "TenantId", "AuthorUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportReply_TenantId_TicketId_CreatedAtUtc",
                table: "SupportReply",
                columns: new[] { "TenantId", "TicketId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceDocument");

            migrationBuilder.DropTable(
                name: "ProductPricing");

            migrationBuilder.DropTable(
                name: "SupportAttachment");

            migrationBuilder.DropTable(
                name: "SupportChatMessage");

            migrationBuilder.DropTable(
                name: "CommerceOrder");

            migrationBuilder.DropTable(
                name: "SupportReply");

            migrationBuilder.DropTable(
                name: "SupportChat");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_SupportTickets_TenantId_CustomerId_Id",
                table: "SupportTickets");

            migrationBuilder.DropIndex(
                name: "IX_SupportTickets_TenantId_CustomerId_CreatedAtUtc",
                table: "SupportTickets");

            migrationBuilder.DropIndex(
                name: "IX_ProductReleases_TenantId_Status_PublishedAtUtc",
                table: "ProductReleases");

            migrationBuilder.DropIndex(
                name: "IX_Entitlements_TenantId_CustomerId_ProductId_ExpiresAtUtc",
                table: "Entitlements");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_TenantId_CustomerId_StartsAtUtc",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "Urgency",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "StoreUrl",
                table: "ProductReleases");

            migrationBuilder.DropColumn(
                name: "SupportExpiresAtUtc",
                table: "Entitlements");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_TenantId_CustomerId",
                table: "SupportTickets",
                columns: new[] { "TenantId", "CustomerId" });
        }
    }
}
