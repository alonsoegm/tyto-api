using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tyto.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignDatabaseConnectionTestFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastTestError",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "DatabaseConnections");

            migrationBuilder.RenameColumn(
                name: "LastTestedAt",
                table: "DatabaseConnections",
                newName: "LastTestDate");

            migrationBuilder.AddColumn<string>(
                name: "LastTestMessage",
                table: "DatabaseConnections",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastTestStatus",
                table: "DatabaseConnections",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastTestStatusCode",
                table: "DatabaseConnections",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastTestMessage",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "LastTestStatus",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "LastTestStatusCode",
                table: "DatabaseConnections");

            migrationBuilder.RenameColumn(
                name: "LastTestDate",
                table: "DatabaseConnections",
                newName: "LastTestedAt");

            migrationBuilder.AddColumn<string>(
                name: "LastTestError",
                table: "DatabaseConnections",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "DatabaseConnections",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }
    }
}
