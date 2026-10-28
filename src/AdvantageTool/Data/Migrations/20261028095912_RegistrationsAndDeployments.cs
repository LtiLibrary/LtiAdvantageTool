using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdvantageTool.Data.Migrations
{
    /// <inheritdoc />
    public partial class RegistrationsAndDeployments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Platforms_Issuer",
                table: "Platforms");

            migrationBuilder.AddColumn<string>(
                name: "DeploymentIds",
                table: "Platforms",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Platforms_Issuer_ClientId",
                table: "Platforms",
                columns: new[] { "Issuer", "ClientId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Platforms_Issuer_ClientId",
                table: "Platforms");

            migrationBuilder.DropColumn(
                name: "DeploymentIds",
                table: "Platforms");

            migrationBuilder.CreateIndex(
                name: "IX_Platforms_Issuer",
                table: "Platforms",
                column: "Issuer",
                unique: true);
        }
    }
}
