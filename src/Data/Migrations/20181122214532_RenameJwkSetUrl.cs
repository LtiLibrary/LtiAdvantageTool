using Microsoft.EntityFrameworkCore.Migrations;

namespace AdvantageTool.Migrations
{
    public partial class RenameJwkSetUrl : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "JsonWebKeySetUrl",
                table: "Platforms",
                newName: "JwkSetUrl");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "JwkSetUrl",
                table: "Platforms",
                newName: "JsonWebKeySetUrl");
        }
    }
}
