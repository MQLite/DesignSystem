using DesignSystem.Domain.Entities;
using DesignSystem.Domain.Enums;

namespace DesignSystem.Infrastructure.Persistence;

public static class AppDbContextSeeder
{
    // A single centered placement slot (final position on canvas, normalised 0..1)
    private const string DefaultSubjectSlots =
        """[{"id":"main-subject","x":0.25,"y":0.15,"w":0.50,"h":0.60,"anchor":"BottomCenter","fitMode":"Contain","allowUserMove":true,"allowUserScale":true,"minScale":0.8,"maxScale":1.4}]""";

    // Safe-zone for text at the bottom
    private const string DefaultTextZones =
        """[{"id":"title","x":0.05,"y":0.02,"w":0.90,"h":0.10},{"id":"footer","x":0.05,"y":0.78,"w":0.90,"h":0.20}]""";

    public static async Task SeedAsync(AppDbContext db)
    {
        if (db.Backgrounds.Any())
            return;

        var backgrounds = new List<Background>
        {
            new()
            {
                Id = new Guid("11111111-0000-0000-0000-000000000001"),
                Name = "Serene Lily — Funeral",
                OccasionType = OccasionType.Funeral,
                IsAiGenerated = false,
                PreviewPath = "storage/backgrounds/seeded/lily_preview.jpg",
                SourcePath  = "storage/backgrounds/seeded/lily_source.png",
                Layouts = new List<BackgroundLayout>
                {
                    new()
                    {
                        Id = new Guid("22222222-0000-0000-0000-000000000001"),
                        SizeCode = "A3",
                        WidthMm  = 297,
                        HeightMm = 420,
                        Orientation = "Portrait",
                        SubjectSlotsJson = DefaultSubjectSlots,
                        TextZonesJson    = DefaultTextZones,
                    }
                }
            },
            new()
            {
                Id = new Guid("11111111-0000-0000-0000-000000000002"),
                Name = "Golden Autumn — Funeral",
                OccasionType = OccasionType.Funeral,
                IsAiGenerated = false,
                PreviewPath = "storage/backgrounds/seeded/autumn_preview.jpg",
                SourcePath  = "storage/backgrounds/seeded/autumn_source.png",
                Layouts = new List<BackgroundLayout>
                {
                    new()
                    {
                        Id = new Guid("22222222-0000-0000-0000-000000000002"),
                        SizeCode = "A3",
                        WidthMm  = 297,
                        HeightMm = 420,
                        Orientation = "Portrait",
                        SubjectSlotsJson = DefaultSubjectSlots,
                        TextZonesJson    = DefaultTextZones,
                    }
                }
            },
            new()
            {
                Id = new Guid("11111111-0000-0000-0000-000000000003"),
                Name = "Peaceful Dove — Funeral",
                OccasionType = OccasionType.Funeral,
                IsAiGenerated = false,
                PreviewPath = "storage/backgrounds/seeded/dove_preview.jpg",
                SourcePath  = "storage/backgrounds/seeded/dove_source.png",
                Layouts = new List<BackgroundLayout>
                {
                    new()
                    {
                        Id = new Guid("22222222-0000-0000-0000-000000000003"),
                        SizeCode = "A4",
                        WidthMm  = 210,
                        HeightMm = 297,
                        Orientation = "Portrait",
                        SubjectSlotsJson = DefaultSubjectSlots,
                        TextZonesJson    = DefaultTextZones,
                    }
                }
            },
        };

        db.Backgrounds.AddRange(backgrounds);
        await db.SaveChangesAsync();
    }
}
