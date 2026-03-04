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
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    OccasionType = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviewPath = table.Column<string>(type: "TEXT", nullable: false),
                    SourcePath = table.Column<string>(type: "TEXT", nullable: false),
                    PromptText = table.Column<string>(type: "TEXT", nullable: true),
                    IsAiGenerated = table.Column<bool>(type: "INTEGER", nullable: false),
                    GenerationSessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Backgrounds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubjectAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OriginalPath = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalWidthPx = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalHeightPx = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalDpi = table.Column<int>(type: "INTEGER", nullable: true),
                    CutoutPath = table.Column<string>(type: "TEXT", nullable: true),
                    CutoutWidthPx = table.Column<int>(type: "INTEGER", nullable: true),
                    CutoutHeightPx = table.Column<int>(type: "INTEGER", nullable: true),
                    MaskPath = table.Column<string>(type: "TEXT", nullable: true),
                    FaceBoundingBoxJson = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubjectAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackgroundLayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BackgroundId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SizeCode = table.Column<string>(type: "TEXT", nullable: false),
                    WidthMm = table.Column<int>(type: "INTEGER", nullable: false),
                    HeightMm = table.Column<int>(type: "INTEGER", nullable: false),
                    Orientation = table.Column<string>(type: "TEXT", nullable: false),
                    SubjectSlotsJson = table.Column<string>(type: "TEXT", nullable: false),
                    TextZonesJson = table.Column<string>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
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
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductType = table.Column<int>(type: "INTEGER", nullable: false),
                    OccasionType = table.Column<int>(type: "INTEGER", nullable: false),
                    BackgroundLayoutId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubjectAssetId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TextConfigJson = table.Column<string>(type: "TEXT", nullable: false),
                    UserAdjustmentsJson = table.Column<string>(type: "TEXT", nullable: true),
                    PreviewPath = table.Column<string>(type: "TEXT", nullable: true),
                    ExportSvgPath = table.Column<string>(type: "TEXT", nullable: true),
                    ExportPdfPath = table.Column<string>(type: "TEXT", nullable: true),
                    ExportPsdPath = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
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
