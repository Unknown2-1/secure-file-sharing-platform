using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VaultShare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInternalFileGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InternalFileGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoredFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedToUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Permission = table.Column<int>(type: "integer", nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalFileGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InternalFileGrants_AspNetUsers_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InternalFileGrants_AspNetUsers_GrantedToUserId",
                        column: x => x.GrantedToUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InternalFileGrants_StoredFiles_StoredFileId",
                        column: x => x.StoredFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InternalFileGrants_GrantedByUserId",
                table: "InternalFileGrants",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalFileGrants_GrantedToUserId_ExpiresAt",
                table: "InternalFileGrants",
                columns: new[] { "GrantedToUserId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InternalFileGrants_StoredFileId_GrantedToUserId_RevokedAt",
                table: "InternalFileGrants",
                columns: new[] { "StoredFileId", "GrantedToUserId", "RevokedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InternalFileGrants");
        }
    }
}
