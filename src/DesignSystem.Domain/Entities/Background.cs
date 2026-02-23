using DesignSystem.Domain.Enums;

namespace DesignSystem.Domain.Entities;

public sealed class Background
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    public OccasionType OccasionType { get; set; }

    // Store paths as relative paths, e.g. "storage/backgrounds/bg001_preview.jpg"
    public string PreviewPath { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // One background asset can have multiple layouts/templates (A3 now, A4/A2 etc later)
    public List<BackgroundLayout> Layouts { get; set; } = new();
}
