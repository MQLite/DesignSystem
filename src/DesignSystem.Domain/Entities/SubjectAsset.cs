using DesignSystem.Domain.Enums;

namespace DesignSystem.Domain.Entities;

public sealed class SubjectAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Original upload
    public string OriginalPath { get; set; } = string.Empty;
    public int OriginalWidthPx { get; set; }
    public int OriginalHeightPx { get; set; }
    public int? OriginalDpi { get; set; }

    // Cutout result (optional in PoC V1)
    public string? CutoutPath { get; set; }
    public int? CutoutWidthPx { get; set; }
    public int? CutoutHeightPx { get; set; }

    public string? MaskPath { get; set; }

    // Optional: normalized bbox in 0..1 (JSON), e.g. {"x":0.3,"y":0.2,"w":0.4,"h":0.4}
    public string? FaceBoundingBoxJson { get; set; }

    public SubjectProcessingStatus Status { get; set; } = SubjectProcessingStatus.Uploaded;
    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}