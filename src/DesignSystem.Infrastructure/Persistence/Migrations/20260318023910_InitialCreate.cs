using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesignSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Backgrounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccasionType = table.Column<int>(type: "int", nullable: false),
                    PreviewPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourcePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PromptText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsAiGenerated = table.Column<bool>(type: "bit", nullable: false),
                    GenerationSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Backgrounds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubjectAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OriginalWidthPx = table.Column<int>(type: "int", nullable: false),
                    OriginalHeightPx = table.Column<int>(type: "int", nullable: false),
                    OriginalDpi = table.Column<int>(type: "int", nullable: true),
                    CutoutPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CutoutWidthPx = table.Column<int>(type: "int", nullable: true),
                    CutoutHeightPx = table.Column<int>(type: "int", nullable: true),
                    MaskPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FaceBoundingBoxJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubjectAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackgroundLayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BackgroundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SizeCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WidthMm = table.Column<int>(type: "int", nullable: false),
                    HeightMm = table.Column<int>(type: "int", nullable: false),
                    Orientation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubjectSlotsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TextZonesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundLayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackgroundLayouts_Backgrounds_BackgroundId",
                        column: x => x.BackgroundId,
                        principalTable: "Backgrounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DesignProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductType = table.Column<int>(type: "int", nullable: false),
                    OccasionType = table.Column<int>(type: "int", nullable: false),
                    BackgroundLayoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TextConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserAdjustmentsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubjectCropStateJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreviewPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExportSvgPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExportPdfPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExportPsdPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesignProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DesignProjects_BackgroundLayouts_BackgroundLayoutId",
                        column: x => x.BackgroundLayoutId,
                        principalTable: "BackgroundLayouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DesignProjects_SubjectAssets_SubjectAssetId",
                        column: x => x.SubjectAssetId,
                        principalTable: "SubjectAssets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundLayouts_BackgroundId",
                table: "BackgroundLayouts",
                column: "BackgroundId");

            migrationBuilder.CreateIndex(
                name: "IX_Backgrounds_OccasionType",
                table: "Backgrounds",
                column: "OccasionType");

            migrationBuilder.CreateIndex(
                name: "IX_DesignProjects_BackgroundLayoutId",
                table: "DesignProjects",
                column: "BackgroundLayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_DesignProjects_CreatedAt",
                table: "DesignProjects",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DesignProjects_Status",
                table: "DesignProjects",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DesignProjects_SubjectAssetId",
                table: "DesignProjects",
                column: "SubjectAssetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DesignProjects");

            migrationBuilder.DropTable(
                name: "BackgroundLayouts");

            migrationBuilder.DropTable(
                name: "SubjectAssets");

            migrationBuilder.DropTable(
                name: "Backgrounds");
        }
    }
}
