using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VaultShare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUploadConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_StoredFiles_FileSize_Positive",
                table: "StoredFiles",
                sql: "\"FileSize\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FileUploads_ExpectedSize_Positive",
                table: "FileUploads",
                sql: "\"ExpectedSize\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FileUploads_Expiry",
                table: "FileUploads",
                sql: "\"ExpiresAt\" > \"CreatedAt\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FileUploads_Offset_Range",
                table: "FileUploads",
                sql: "\"UploadOffset\" >= 0 AND \"UploadOffset\" <= \"ExpectedSize\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StoredFiles_FileSize_Positive",
                table: "StoredFiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FileUploads_ExpectedSize_Positive",
                table: "FileUploads");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FileUploads_Expiry",
                table: "FileUploads");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FileUploads_Offset_Range",
                table: "FileUploads");
        }
    }
}
