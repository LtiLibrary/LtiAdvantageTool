using Microsoft.EntityFrameworkCore.Migrations;

namespace AdvantageTool.Data.Migrations
{
    public partial class ChangeClient : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrivateKey",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PublicKey",
                table: "Clients");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrivateKey",
                table: "Clients",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PublicKey",
                table: "Clients",
                nullable: false,
                defaultValue: "");
        }
    }
}
