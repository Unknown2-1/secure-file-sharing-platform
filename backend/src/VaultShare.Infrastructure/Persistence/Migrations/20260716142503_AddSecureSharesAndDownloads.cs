using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VaultShare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecureSharesAndDownloads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Shares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicIdentifier = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SecretTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreationIdempotencyKeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PasswordHash = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MaximumDownloads = table.Column<int>(type: "integer", nullable: true),
                    DownloadCount = table.Column<int>(type: "integer", nullable: false),
                    IsOneTime = table.Column<bool>(type: "boolean", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    AllowPreview = table.Column<bool>(type: "boolean", nullable: false),
                    RequireEmailVerification = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastAccessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shares_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Shares_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DownloadSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShareId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DownloadSessions_Shares_ShareId",
                        column: x => x.ShareId,
                        principalTable: "Shares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShareItems",
                columns: table => new
                {
                    ShareId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoredFileId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShareItems", x => new { x.ShareId, x.StoredFileId });
                    table.ForeignKey(
                        name: "FK_ShareItems_Shares_ShareId",
                        column: x => x.ShareId,
                        principalTable: "Shares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShareItems_StoredFiles_StoredFileId",
                        column: x => x.StoredFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FileDownloads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShareId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoredFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileDownloads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileDownloads_DownloadSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "DownloadSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FileDownloads_Shares_ShareId",
                        column: x => x.ShareId,
                        principalTable: "Shares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FileDownloads_StoredFiles_StoredFileId",
                        column: x => x.StoredFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadSessions_ShareId_ExpiresAt",
                table: "DownloadSessions",
                columns: new[] { "ShareId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadSessions_TokenHash",
                table: "DownloadSessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileDownloads_SessionId",
                table: "FileDownloads",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_FileDownloads_ShareId",
                table: "FileDownloads",
                column: "ShareId");

            migrationBuilder.CreateIndex(
                name: "IX_FileDownloads_StartedAt",
                table: "FileDownloads",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FileDownloads_StoredFileId",
                table: "FileDownloads",
                column: "StoredFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareItems_StoredFileId",
                table: "ShareItems",
                column: "StoredFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Shares_CreatedAt",
                table: "Shares",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Shares_CreatedByUserId_CreationIdempotencyKeyHash",
                table: "Shares",
                columns: new[] { "CreatedByUserId", "CreationIdempotencyKeyHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shares_ExpiresAt",
                table: "Shares",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Shares_IsRevoked",
                table: "Shares",
                column: "IsRevoked");

            migrationBuilder.CreateIndex(
                name: "IX_Shares_PublicIdentifier",
                table: "Shares",
                column: "PublicIdentifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shares_SecretTokenHash",
                table: "Shares",
                column: "SecretTokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_Shares_WorkspaceId",
                table: "Shares",
                column: "WorkspaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileDownloads");

            migrationBuilder.DropTable(
                name: "ShareItems");

            migrationBuilder.DropTable(
                name: "DownloadSessions");

            migrationBuilder.DropTable(
                name: "Shares");
        }
    }
}
