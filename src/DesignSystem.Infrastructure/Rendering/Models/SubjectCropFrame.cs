namespace DesignSystem.Infrastructure.Rendering.Models;

/// <summary>
/// Defines a crop window on the canvas that the user sees when adjusting the subject photo.
/// Parsed from BackgroundLayout.SubjectCropFramesJson.
/// All coordinates are normalised to 0..1 (fraction of canvas width/height).
/// </summary>
public record SubjectCropFrame
{
    /// <summary>Stable identifier referenced by SubjectCropStateJson entries.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Crop window position and size on the canvas (normalised 0..1).</summary>
    public RectN Rect { get; init; } = new(0, 0, 1, 1);

    /// <summary>
    /// Shape of the crop mask. "rect" (default) | "circle" | "oval"
    /// </summary>
    public string Shape { get; init; } = "rect";

    /// <summary>
    /// Optional locked aspect ratio (width / height). Null = free aspect.
    /// e.g. 0.857 ≈ 6:7 portrait, 1.0 = square
    /// </summary>
    public double? AspectRatio { get; init; }

    /// <summary>Whether the user can pan within the crop frame.</summary>
    public bool AllowUserMove { get; init; } = true;

    /// <summary>Whether the user can zoom within the crop frame.</summary>
    public bool AllowUserScale { get; init; } = true;
}
