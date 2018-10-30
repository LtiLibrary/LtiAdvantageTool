using Microsoft.EntityFrameworkCore.Migrations;

namespace AdvantageTool.Data.Migrations
{
    public partial class AddPlatformPrefix : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "JsonWebKeysUrl",
                table: "Clients",
                newName: "PlatformJsonWebKeysUrl");

            migrationBuilder.RenameColumn(
                name: "Issuer",
                table: "Clients",
                newName: "PlatformIssuer");

            migrationBuilder.RenameColumn(
                name: "AccessTokenUrl",
                table: "Clients",
                newName: "PlatformAccessTokenUrl");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PlatformJsonWebKeysUrl",
                table: "Clients",
                newName: "JsonWebKeysUrl");

            migrationBuilder.RenameColumn(
                name: "PlatformIssuer",
                table: "Clients",
                newName: "Issuer");

            migrationBuilder.RenameColumn(
                name: "PlatformAccessTokenUrl",
                table: "Clients",
                newName: "AccessTokenUrl");
        }
    }
}
