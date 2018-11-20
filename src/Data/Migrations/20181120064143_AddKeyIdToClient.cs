using Microsoft.EntityFrameworkCore.Migrations;

namespace AdvantageTool.Migrations
{
    public partial class AddKeyIdToClient : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KeyId",
                table: "Clients",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KeyId",
                table: "Clients");
        }
    }
}
