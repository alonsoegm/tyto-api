using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tyto.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Changes = table.Column<string>(type: "text", nullable: true),
                    PerformedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PerformedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DatabaseConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ConnectionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LastTestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastTestError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SF_AuthMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SF_InstanceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SF_Username = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SF_ConsumerKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SF_ClientSecret = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SF_SigningKeySource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SF_PrivateKeyFile = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    SF_Passphrase = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SF_IsSandbox = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DV_AuthMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DV_EnvironmentUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DV_TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DV_ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DV_ClientSecret = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DV_CertificateSource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DV_CertificateData = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    DV_CertificateThumbprint = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DV_KeyVaultUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DV_KeyVaultCertificateName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DV_ManagedIdentityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DV_UserAssignedClientId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatabaseConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ModelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApiVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AuthenticationMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ApiKeyEncrypted = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ManagedIdentityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UserAssignedClientId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentModels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LanguageModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ServiceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DeploymentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApiVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AuthenticationMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ApiKeyEncrypted = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ManagedIdentityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UserAssignedClientId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LanguageModels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Configurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExtractionStrategy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ModelSelectionMode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LanguageModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentModelId = table.Column<Guid>(type: "uuid", nullable: true),
                    DatabaseConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetObject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SystemPrompt = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    UserPromptTemplate = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    MaxTokens = table.Column<int>(type: "integer", nullable: false),
                    Temperature = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Configurations_DatabaseConnections_DatabaseConnectionId",
                        column: x => x.DatabaseConnectionId,
                        principalTable: "DatabaseConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Configurations_DocumentModels_DocumentModelId",
                        column: x => x.DocumentModelId,
                        principalTable: "DocumentModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Configurations_LanguageModels_LanguageModelId",
                        column: x => x.LanguageModelId,
                        principalTable: "LanguageModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MappedFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FieldType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RequirementLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExtractionHint = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DefaultValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ParentFieldId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MappedFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MappedFields_Configurations_ConfigurationId",
                        column: x => x.ConfigurationId,
                        principalTable: "Configurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MappedFields_MappedFields_ParentFieldId",
                        column: x => x.ParentFieldId,
                        principalTable: "MappedFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RunHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DocumentsProcessed = table.Column<int>(type: "integer", nullable: false),
                    RecordsCreated = table.Column<int>(type: "integer", nullable: false),
                    RecordsUpdated = table.Column<int>(type: "integer", nullable: false),
                    RecordsFailed = table.Column<int>(type: "integer", nullable: false),
                    RawInput = table.Column<string>(type: "text", nullable: true),
                    RawOutput = table.Column<string>(type: "text", nullable: true),
                    TriggeredBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RunHistories_Configurations_ConfigurationId",
                        column: x => x.ConfigurationId,
                        principalTable: "Configurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_PerformedAt",
                table: "AuditLogs",
                column: "PerformedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_DatabaseConnectionId",
                table: "Configurations",
                column: "DatabaseConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_DocumentModelId",
                table: "Configurations",
                column: "DocumentModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_LanguageModelId",
                table: "Configurations",
                column: "LanguageModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_Name",
                table: "Configurations",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseConnections_Name",
                table: "DatabaseConnections",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentModels_Name",
                table: "DocumentModels",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LanguageModels_Name",
                table: "LanguageModels",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MappedFields_ConfigurationId",
                table: "MappedFields",
                column: "ConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_MappedFields_ParentFieldId",
                table: "MappedFields",
                column: "ParentFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_RunHistories_ConfigurationId",
                table: "RunHistories",
                column: "ConfigurationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "MappedFields");

            migrationBuilder.DropTable(
                name: "RunHistories");

            migrationBuilder.DropTable(
                name: "Configurations");

            migrationBuilder.DropTable(
                name: "DatabaseConnections");

            migrationBuilder.DropTable(
                name: "DocumentModels");

            migrationBuilder.DropTable(
                name: "LanguageModels");
        }
    }
}
