using Microsoft.EntityFrameworkCore.Migrations;

namespace AdvantageTool.Data.Migrations
{
    public partial class AddClientPrivateKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PlatformJsonWebKeysUrl",
                table: "Platforms",
                newName: "JsonWebKeysUrl");

            migrationBuilder.RenameColumn(
                name: "PlatformIssuer",
                table: "Platforms",
                newName: "Issuer");

            migrationBuilder.RenameColumn(
                name: "PlatformAccessTokenUrl",
                table: "Platforms",
                newName: "ClientPrivateKey");

            migrationBuilder.AddColumn<string>(
                name: "AccessTokenUrl",
                table: "Platforms",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessTokenUrl",
                table: "Platforms");

            migrationBuilder.RenameColumn(
                name: "JsonWebKeysUrl",
                table: "Platforms",
                newName: "PlatformJsonWebKeysUrl");

            migrationBuilder.RenameColumn(
                name: "Issuer",
                table: "Platforms",
                newName: "PlatformIssuer");

            migrationBuilder.RenameColumn(
                name: "ClientPrivateKey",
                table: "Platforms",
                newName: "PlatformAccessTokenUrl");
        }
    }
}
