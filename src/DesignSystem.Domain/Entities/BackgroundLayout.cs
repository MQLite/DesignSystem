namespace DesignSystem.Domain.Entities;

/// <summary>
/// A layout/template definition for a background asset.
/// Although the current PoC uses A3 Portrait only, this model is extensible to other sizes.
/// </summary>
public sealed class BackgroundLayout
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BackgroundId { get; set; }
    public Background? Background { get; set; }

    /// <summary>
    /// A human-friendly size code, e.g. "A3", "A4", "Custom".
    /// </summary>
    public string SizeCode { get; set; } = "A3";

    /// <summary>
    /// Physical dimensions in millimeters. For A3 portrait: 297 x 420.
    /// (Orientation is represented by WidthMm/HeightMm + Orientation field)
    /// </summary>
    public int WidthMm { get; set; } = 297;
    public int HeightMm { get; set; } = 420;

    /// <summary>
    /// "Portrait" / "Landscape"
    /// </summary>
    public string Orientation { get; set; } = "Portrait";

    /// <summary>
    /// JSON stored as TEXT in SQLite.
    /// Defines where and how the subject image is placed on the final canvas.
    /// Expected to include normalized slot rects (x,y,w,h within 0..1).
    /// </summary>
    public string SubjectSlotsJson { get; set; } = "[]";

    /// <summary>
    /// JSON stored as TEXT in SQLite. Nullable — omit when no cropping is needed.
    /// Defines the crop window(s) shown to the user when they adjust the subject photo,
    /// using the same 0..1 normalized coordinate space as SubjectSlotsJson.
    /// Each frame has an optional aspectRatio, shape, and allowUserMove/Scale flags.
    /// Example: [{"id":"main-crop","x":0.20,"y":0.10,"w":0.60,"h":0.70,"shape":"rect",
    ///            "aspectRatio":0.857,"allowUserMove":true,"allowUserScale":true}]
    /// </summary>
    public string? SubjectCropFramesJson { get; set; }

    /// <summary>
    /// Optional JSON for text safe zones etc.
    /// </summary>
    public string? TextZonesJson { get; set; }

    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
