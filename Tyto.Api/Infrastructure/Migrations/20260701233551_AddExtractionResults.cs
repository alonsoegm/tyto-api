using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tyto.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExtractionResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExtractionResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunHistoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExtractedData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LanguageModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DocumentModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractionResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExtractionResults_Configurations_ConfigurationId",
                        column: x => x.ConfigurationId,
                        principalTable: "Configurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExtractionResults_RunHistories_RunHistoryId",
                        column: x => x.RunHistoryId,
                        principalTable: "RunHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExtractionResults_ConfigurationId",
                table: "ExtractionResults",
                column: "ConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExtractionResults_RunHistoryId",
                table: "ExtractionResults",
                column: "RunHistoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExtractionResults");
        }
    }
}
