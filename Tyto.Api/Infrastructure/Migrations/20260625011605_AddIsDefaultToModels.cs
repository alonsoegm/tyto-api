using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tyto.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDefaultToModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "LanguageModels",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "DocumentModels",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill: promote the oldest existing model of each type to default so the
            // "a default always exists once any model exists" invariant holds for existing data.
            migrationBuilder.Sql(
                "UPDATE \"LanguageModels\" SET \"IsDefault\" = true " +
                "WHERE \"Id\" = (SELECT \"Id\" FROM \"LanguageModels\" ORDER BY \"CreatedAt\", \"Id\" LIMIT 1);");

            migrationBuilder.Sql(
                "UPDATE \"DocumentModels\" SET \"IsDefault\" = true " +
                "WHERE \"Id\" = (SELECT \"Id\" FROM \"DocumentModels\" ORDER BY \"CreatedAt\", \"Id\" LIMIT 1);");

            migrationBuilder.CreateIndex(
                name: "IX_LanguageModels_IsDefault",
                table: "LanguageModels",
                column: "IsDefault",
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentModels_IsDefault",
                table: "DocumentModels",
                column: "IsDefault",
                unique: true,
                filter: "\"IsDefault\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LanguageModels_IsDefault",
                table: "LanguageModels");

            migrationBuilder.DropIndex(
                name: "IX_DocumentModels_IsDefault",
                table: "DocumentModels");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "LanguageModels");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "DocumentModels");
        }
    }
}
