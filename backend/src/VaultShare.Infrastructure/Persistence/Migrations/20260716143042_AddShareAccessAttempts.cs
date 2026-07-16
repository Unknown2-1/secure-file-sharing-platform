using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VaultShare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShareAccessAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShareAccessAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShareId = table.Column<Guid>(type: "uuid", nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    ResultCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IpAddressHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShareAccessAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShareAccessAttempts_Shares_ShareId",
                        column: x => x.ShareId,
                        principalTable: "Shares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShareAccessAttempts_IpAddressHash",
                table: "ShareAccessAttempts",
                column: "IpAddressHash");

            migrationBuilder.CreateIndex(
                name: "IX_ShareAccessAttempts_ShareId_AttemptedAt",
                table: "ShareAccessAttempts",
                columns: new[] { "ShareId", "AttemptedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShareAccessAttempts");
        }
    }
}
