using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tyto.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigurationUploadSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcceptedFileTypes",
                table: "Configurations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "MaxUploadSizeMB",
                table: "Configurations",
                type: "integer",
                nullable: false,
                defaultValue: 25);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptedFileTypes",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "MaxUploadSizeMB",
                table: "Configurations");
        }
    }
}
