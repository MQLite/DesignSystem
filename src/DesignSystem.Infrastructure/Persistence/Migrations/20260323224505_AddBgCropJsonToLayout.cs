using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesignSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBgCropJsonToLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BgCropJson",
                table: "BackgroundLayouts",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BgCropJson",
                table: "BackgroundLayouts");
        }
    }
}
