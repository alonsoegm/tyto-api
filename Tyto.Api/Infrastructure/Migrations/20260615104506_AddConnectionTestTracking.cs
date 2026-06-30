using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tyto.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectionTestTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastTestDate",
                table: "DocumentModels",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastTestStatus",
                table: "DocumentModels",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastTestStatusCode",
                table: "DocumentModels",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastTestDate",
                table: "DocumentModels");

            migrationBuilder.DropColumn(
                name: "LastTestStatus",
                table: "DocumentModels");

            migrationBuilder.DropColumn(
                name: "LastTestStatusCode",
                table: "DocumentModels");
        }
    }
}
