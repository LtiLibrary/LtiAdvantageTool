using Microsoft.EntityFrameworkCore.Migrations;

namespace AdvantageTool.Migrations
{
    public partial class RemoveUserIdFromPlatform : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Platforms_AspNetUsers_AdvantageToolUserId",
                table: "Platforms");

            migrationBuilder.DropIndex(
                name: "IX_Platforms_AdvantageToolUserId",
                table: "Platforms");

            migrationBuilder.DropColumn(
                name: "AdvantageToolUserId",
                table: "Platforms");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Platforms",
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Platforms_UserId",
                table: "Platforms",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Platforms_AspNetUsers_UserId",
                table: "Platforms",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Platforms_AspNetUsers_UserId",
                table: "Platforms");

            migrationBuilder.DropIndex(
                name: "IX_Platforms_UserId",
                table: "Platforms");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Platforms",
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdvantageToolUserId",
                table: "Platforms",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Platforms_AdvantageToolUserId",
                table: "Platforms",
                column: "AdvantageToolUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Platforms_AspNetUsers_AdvantageToolUserId",
                table: "Platforms",
                column: "AdvantageToolUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
