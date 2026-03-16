using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesignSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubjectCropSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // BackgroundLayout: crop frame definition(s) for the subject photo
            migrationBuilder.AddColumn<string>(
                name: "SubjectCropFramesJson",
                table: "BackgroundLayouts",
                type: "TEXT",
                nullable: true);

            // DesignProject: user-applied crop pan/zoom state (keyed by crop frame id)
            migrationBuilder.AddColumn<string>(
                name: "SubjectCropStateJson",
                table: "DesignProjects",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubjectCropFramesJson",
                table: "BackgroundLayouts");

            migrationBuilder.DropColumn(
                name: "SubjectCropStateJson",
                table: "DesignProjects");
        }
    }
}
