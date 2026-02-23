namespace DesignSystem.Domain.Entities;

public sealed class SubjectAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string OriginalPath { get; set; } = string.Empty;
    public string? CutoutPath { get; set; }
    public string? MaskPath { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
