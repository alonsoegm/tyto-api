using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tyto.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseConnectionSalesforceAndEnumChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SF_ApiVersion",
                table: "DatabaseConnections",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SF_JwtAudience",
                table: "DatabaseConnections",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SF_KeyVaultSecretName",
                table: "DatabaseConnections",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SF_KeyVaultUrl",
                table: "DatabaseConnections",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SF_RunAsIntegrationUser",
                table: "DatabaseConnections",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SF_ApiVersion",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "SF_JwtAudience",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "SF_KeyVaultSecretName",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "SF_KeyVaultUrl",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "SF_RunAsIntegrationUser",
                table: "DatabaseConnections");
        }
    }
}
