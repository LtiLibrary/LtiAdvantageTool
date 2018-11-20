using Microsoft.EntityFrameworkCore.Migrations;

namespace AdvantageTool.Migrations
{
    public partial class FixTypoInJwks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "JsonWebKeysUrl",
                table: "Platforms",
                newName: "JsonWebKeySetUrl");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "JsonWebKeySetUrl",
                table: "Platforms",
                newName: "JsonWebKeysUrl");
        }
    }
}
