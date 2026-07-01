using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tyto.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorDatabaseConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DV_AuthMethod",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "DV_CertificateData",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "DV_CertificateSource",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "DV_CertificateThumbprint",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "DV_ClientId",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "DV_ClientSecret",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "DV_EnvironmentUrl",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "DV_KeyVaultCertificateName",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "DV_KeyVaultUrl",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "DV_ManagedIdentityType",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "DV_TenantId",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "DV_UserAssignedClientId",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "SF_ApiVersion",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "SF_AuthMethod",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "SF_ClientSecret",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "SF_ConsumerKey",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "SF_InstanceUrl",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "SF_IsSandbox",
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
                name: "SF_Passphrase",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "SF_PrivateKeyFile",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "SF_RunAsIntegrationUser",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "SF_SigningKeySource",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "SF_Username",
                table: "DatabaseConnections");

            migrationBuilder.AddColumn<string>(
                name: "Config",
                table: "DatabaseConnections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInternal",
                table: "DatabaseConnections",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.InsertData(
                table: "DatabaseConnections",
                columns: new[] { "Id", "Config", "ConnectionType", "CreatedAt", "CreatedBy", "Description", "IsInternal", "LastTestDate", "LastTestMessage", "LastTestStatus", "LastTestStatusCode", "Name", "UpdatedAt", "UpdatedBy" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), null, "InternalSql", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "System-managed Azure SQL destination. Extracted data is stored automatically.", true, null, null, null, null, "Tyto Internal", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "DatabaseConnections",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.DropColumn(
                name: "Config",
                table: "DatabaseConnections");

            migrationBuilder.DropColumn(
                name: "IsInternal",
                table: "DatabaseConnections");

            migrationBuilder.AddColumn<string>(
                name: "DV_AuthMethod",
                table: "DatabaseConnections",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DV_CertificateData",
                table: "DatabaseConnections",
                type: "nvarchar(max)",
                maxLength: 10000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DV_CertificateSource",
                table: "DatabaseConnections",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DV_CertificateThumbprint",
                table: "DatabaseConnections",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DV_ClientId",
                table: "DatabaseConnections",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DV_ClientSecret",
                table: "DatabaseConnections",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DV_EnvironmentUrl",
                table: "DatabaseConnections",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DV_KeyVaultCertificateName",
                table: "DatabaseConnections",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DV_KeyVaultUrl",
                table: "DatabaseConnections",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DV_ManagedIdentityType",
                table: "DatabaseConnections",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DV_TenantId",
                table: "DatabaseConnections",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DV_UserAssignedClientId",
                table: "DatabaseConnections",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SF_ApiVersion",
                table: "DatabaseConnections",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SF_AuthMethod",
                table: "DatabaseConnections",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SF_ClientSecret",
                table: "DatabaseConnections",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SF_ConsumerKey",
                table: "DatabaseConnections",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SF_InstanceUrl",
                table: "DatabaseConnections",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SF_IsSandbox",
                table: "DatabaseConnections",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SF_JwtAudience",
                table: "DatabaseConnections",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SF_KeyVaultSecretName",
                table: "DatabaseConnections",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SF_KeyVaultUrl",
                table: "DatabaseConnections",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SF_Passphrase",
                table: "DatabaseConnections",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SF_PrivateKeyFile",
                table: "DatabaseConnections",
                type: "nvarchar(max)",
                maxLength: 5000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SF_RunAsIntegrationUser",
                table: "DatabaseConnections",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SF_SigningKeySource",
                table: "DatabaseConnections",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SF_Username",
                table: "DatabaseConnections",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }
    }
}
