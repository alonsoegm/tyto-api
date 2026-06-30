using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tyto.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLanguageModelTestFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiSurface",
                table: "LanguageModels",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastTestDate",
                table: "LanguageModels",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastTestMessage",
                table: "LanguageModels",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastTestStatus",
                table: "LanguageModels",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastTestStatusCode",
                table: "LanguageModels",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelName",
                table: "LanguageModels",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastTestMessage",
                table: "DocumentModels",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiSurface",
                table: "LanguageModels");

            migrationBuilder.DropColumn(
                name: "LastTestDate",
                table: "LanguageModels");

            migrationBuilder.DropColumn(
                name: "LastTestMessage",
                table: "LanguageModels");

            migrationBuilder.DropColumn(
                name: "LastTestStatus",
                table: "LanguageModels");

            migrationBuilder.DropColumn(
                name: "LastTestStatusCode",
                table: "LanguageModels");

            migrationBuilder.DropColumn(
                name: "ModelName",
                table: "LanguageModels");

            migrationBuilder.DropColumn(
                name: "LastTestMessage",
                table: "DocumentModels");
        }
    }
}
