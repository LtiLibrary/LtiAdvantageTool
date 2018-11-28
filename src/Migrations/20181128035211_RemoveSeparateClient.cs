using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AdvantageTool.Migrations
{
    public partial class RemoveSeparateClient : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.AddColumn<string>(
                name: "ClientId",
                table: "Platforms",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KeyId",
                table: "Platforms",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivateKey",
                table: "Platforms",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "Platforms");

            migrationBuilder.DropColumn(
                name: "KeyId",
                table: "Platforms");

            migrationBuilder.DropColumn(
                name: "PrivateKey",
                table: "Platforms");

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    ClientId = table.Column<string>(nullable: false),
                    KeyId = table.Column<string>(nullable: true),
                    PrivateKey = table.Column<string>(nullable: false),
                    PublicKey = table.Column<string>(nullable: false),
                    UserId = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clients_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_UserId",
                table: "Clients",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL");
        }
    }
}
