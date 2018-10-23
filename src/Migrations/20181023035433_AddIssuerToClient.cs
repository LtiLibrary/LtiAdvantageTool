using Microsoft.EntityFrameworkCore.Migrations;

namespace AdvantageTool.Data.Migrations
{
    public partial class AddIssuerToClient : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessTokenUrl",
                table: "Clients",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Issuer",
                table: "Clients",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "JsonWebKeysUrl",
                table: "Clients",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessTokenUrl",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Issuer",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "JsonWebKeysUrl",
                table: "Clients");
        }
    }
}
