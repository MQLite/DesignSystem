using DesignSystem.Domain.Enums;

namespace DesignSystem.Domain.Entities;

public sealed class DesignProject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ProductType ProductType { get; set; }

    public OccasionType OccasionType { get; set; }

    public Guid BackgroundLayoutId { get; set; }
    public BackgroundLayout? BackgroundLayout { get; set; }

    public Guid? SubjectAssetId { get; set; }
    public SubjectAsset? SubjectAsset { get; set; }

    // JSON stored as TEXT in SQLite (top title, name, dates, message, font/effects, etc.)
    public string TextConfigJson { get; set; } = "{}";

    // JSON stored as TEXT in SQLite (offset/scale, etc.)
    public string? UserAdjustmentsJson { get; set; }

    /// <summary>
    /// JSON stored as TEXT in SQLite. Nullable — populated when the user adjusts the crop frame.
    /// Stores per-frame crop pan/zoom state, keyed by SubjectCropFrame.Id.
    /// Intentionally separate from UserAdjustmentsJson (which holds whole-canvas CanvasLayout transforms).
    /// Example: [{"cropFrameId":"main-crop","offsetX":0.0,"offsetY":0.0,"scale":1.0}]
    /// </summary>
    public string? SubjectCropStateJson { get; set; }

    public string? PreviewPath { get; set; }
    public string? ExportSvgPath { get; set; }
    public string? ExportPdfPath { get; set; }
    public string? ExportPsdPath { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Draft;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
