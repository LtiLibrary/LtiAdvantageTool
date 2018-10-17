using Microsoft.EntityFrameworkCore.Migrations;

namespace AdvantageTool.Data.Migrations
{
    public partial class AddPlatforms : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Platforms",
                columns: table => new
                {
                    Id = table.Column<string>(nullable: false),
                    PublicKey = table.Column<string>(nullable: false),
                    ContactEmail = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true),
                    Guid = table.Column<string>(nullable: true),
                    Name = table.Column<string>(nullable: false),
                    ProductFamilyCode = table.Column<string>(nullable: true),
                    Url = table.Column<string>(nullable: true),
                    Version = table.Column<string>(nullable: true),
                    UserId = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Platforms", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Platforms");
        }
    }
}
