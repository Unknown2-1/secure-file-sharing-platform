using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VaultShare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkspaceSettings",
                columns: table => new
                {
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageQuotaBytes = table.Column<long>(type: "bigint", nullable: false),
                    AuditRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    DeletedFileGraceDays = table.Column<int>(type: "integer", nullable: false),
                    AllowMemberPublicShares = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceSettings", x => x.WorkspaceId);
                    table.CheckConstraint("CK_WorkspaceSettings_AuditRetention", "\"AuditRetentionDays\" BETWEEN 30 AND 3650");
                    table.CheckConstraint("CK_WorkspaceSettings_DeleteGrace", "\"DeletedFileGraceDays\" BETWEEN 0 AND 365");
                    table.CheckConstraint("CK_WorkspaceSettings_Quota", "\"StorageQuotaBytes\" >= 1048576");
                    table.ForeignKey(
                        name: "FK_WorkspaceSettings_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkspaceSettings_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceSettings_UpdatedByUserId",
                table: "WorkspaceSettings",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkspaceSettings");
        }
    }
}
