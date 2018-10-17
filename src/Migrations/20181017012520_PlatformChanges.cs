using Microsoft.EntityFrameworkCore.Migrations;

namespace AdvantageTool.Data.Migrations
{
    public partial class PlatformChanges : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublicKey",
                table: "Platforms");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Platforms",
                nullable: true,
                oldClrType: typeof(string));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Platforms",
                nullable: false,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicKey",
                table: "Platforms",
                nullable: false,
                defaultValue: "");
        }
    }
}
