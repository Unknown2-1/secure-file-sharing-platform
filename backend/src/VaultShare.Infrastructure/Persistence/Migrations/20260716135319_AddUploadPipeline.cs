using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VaultShare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUploadPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StoredFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFilename = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SafeDisplayFilename = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StoredObjectKey = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    DetectedMimeType = table.Column<string>(type: "character varying(127)", maxLength: 127, nullable: true),
                    ClientMimeType = table.Column<string>(type: "character varying(127)", maxLength: 127, nullable: false),
                    Sha256Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UploadStatus = table.Column<int>(type: "integer", nullable: false),
                    MalwareScanStatus = table.Column<int>(type: "integer", nullable: false),
                    EncryptionStatus = table.Column<int>(type: "integer", nullable: false),
                    AvailabilityStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PurgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    MetadataVersion = table.Column<int>(type: "integer", nullable: false),
                    EncryptionMetadataId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoredFiles_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoredFiles_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FileUploads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoredFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemporaryPath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ExpectedSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadOffset = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileUploads_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FileUploads_StoredFiles_StoredFileId",
                        column: x => x.StoredFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FileUploads_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileUploads_ExpiresAt",
                table: "FileUploads",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_FileUploads_Status",
                table: "FileUploads",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FileUploads_StoredFileId",
                table: "FileUploads",
                column: "StoredFileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileUploads_UserId_IdempotencyKey",
                table: "FileUploads",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileUploads_WorkspaceId",
                table: "FileUploads",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_CreatedAt",
                table: "StoredFiles",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_DeletedAt",
                table: "StoredFiles",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_EncryptionStatus",
                table: "StoredFiles",
                column: "EncryptionStatus");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_MalwareScanStatus",
                table: "StoredFiles",
                column: "MalwareScanStatus");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_OwnerUserId",
                table: "StoredFiles",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_PurgedAt",
                table: "StoredFiles",
                column: "PurgedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_StoredObjectKey",
                table: "StoredFiles",
                column: "StoredObjectKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_UploadStatus",
                table: "StoredFiles",
                column: "UploadStatus");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_WorkspaceId",
                table: "StoredFiles",
                column: "WorkspaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileUploads");

            migrationBuilder.DropTable(
                name: "StoredFiles");
        }
    }
}
