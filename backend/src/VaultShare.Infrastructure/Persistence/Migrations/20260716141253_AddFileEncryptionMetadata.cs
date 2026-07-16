using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VaultShare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFileEncryptionMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileEncryptionMetadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Algorithm = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AlgorithmVersion = table.Column<int>(type: "integer", nullable: false),
                    WrappedDataKey = table.Column<byte[]>(type: "bytea", nullable: false),
                    KeyWrapNonce = table.Column<byte[]>(type: "bytea", nullable: false),
                    KeyWrapAuthenticationTag = table.Column<byte[]>(type: "bytea", nullable: false),
                    KeyProvider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    KeyIdentifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ChunkSize = table.Column<int>(type: "integer", nullable: false),
                    BaseNonce = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RotatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EncryptionMetadataVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileEncryptionMetadata", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_EncryptionMetadataId",
                table: "StoredFiles",
                column: "EncryptionMetadataId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileEncryptionMetadata_CreatedAt",
                table: "FileEncryptionMetadata",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FileEncryptionMetadata_KeyIdentifier",
                table: "FileEncryptionMetadata",
                column: "KeyIdentifier");

            migrationBuilder.AddForeignKey(
                name: "FK_StoredFiles_FileEncryptionMetadata_EncryptionMetadataId",
                table: "StoredFiles",
                column: "EncryptionMetadataId",
                principalTable: "FileEncryptionMetadata",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StoredFiles_FileEncryptionMetadata_EncryptionMetadataId",
                table: "StoredFiles");

            migrationBuilder.DropTable(
                name: "FileEncryptionMetadata");

            migrationBuilder.DropIndex(
                name: "IX_StoredFiles_EncryptionMetadataId",
                table: "StoredFiles");
        }
    }
}
