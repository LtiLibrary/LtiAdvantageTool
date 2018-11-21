using Microsoft.EntityFrameworkCore.Migrations;

namespace AdvantageTool.Migrations
{
    public partial class RemoveClientFromPlatform : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "Platforms");

            migrationBuilder.DropColumn(
                name: "ClientPrivateKey",
                table: "Platforms");

            migrationBuilder.DropColumn(
                name: "ClientPublicKey",
                table: "Platforms");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientId",
                table: "Platforms",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientPrivateKey",
                table: "Platforms",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientPublicKey",
                table: "Platforms",
                nullable: true);
        }
    }
}
