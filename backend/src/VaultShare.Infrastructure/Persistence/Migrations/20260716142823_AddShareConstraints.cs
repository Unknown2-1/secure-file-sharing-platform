using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VaultShare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShareConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Shares_DownloadCount",
                table: "Shares",
                sql: "\"DownloadCount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Shares_Expiry",
                table: "Shares",
                sql: "\"ExpiresAt\" > \"CreatedAt\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Shares_MaximumDownloads",
                table: "Shares",
                sql: "\"MaximumDownloads\" IS NULL OR \"MaximumDownloads\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DownloadSessions_Expiry",
                table: "DownloadSessions",
                sql: "\"ExpiresAt\" > \"CreatedAt\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Shares_DownloadCount",
                table: "Shares");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Shares_Expiry",
                table: "Shares");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Shares_MaximumDownloads",
                table: "Shares");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DownloadSessions_Expiry",
                table: "DownloadSessions");
        }
    }
}
