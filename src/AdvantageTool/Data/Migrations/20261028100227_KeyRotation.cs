using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdvantageTool.Data.Migrations
{
    /// <inheritdoc />
    public partial class KeyRotation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreviousKeyId",
                table: "Platforms",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousPublicKey",
                table: "Platforms",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviousKeyId",
                table: "Platforms");

            migrationBuilder.DropColumn(
                name: "PreviousPublicKey",
                table: "Platforms");
        }
    }
}
