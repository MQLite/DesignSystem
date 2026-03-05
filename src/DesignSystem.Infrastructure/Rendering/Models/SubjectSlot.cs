namespace DesignSystem.Infrastructure.Rendering.Models;

/// <summary>
/// A parsed slot definition from BackgroundLayout.SubjectSlotsJson.
/// Controls where and how a subject image is placed on the canvas.
/// </summary>
public record SubjectSlot
{
    public string Id { get; init; } = string.Empty;

    /// <summary>Normalized position and size within the canvas (0..1).</summary>
    public RectN Rect { get; init; } = new(0, 0, 1, 1);

    /// <summary>
    /// Anchor point for placement inside the slot.
    /// Supported: "BottomCenter" | "Center" | "TopCenter"
    /// </summary>
    public string Anchor { get; init; } = "BottomCenter";

    /// <summary>
    /// Scaling strategy. Supported: "Contain" | "Cover" | "Stretch"
    /// </summary>
    public string FitMode { get; init; } = "Contain";

    public bool AllowUserMove { get; init; } = true;
    public bool AllowUserScale { get; init; } = true;

    /// <summary>Minimum user-applied scale multiplier (relative to fitted size).</summary>
    public double MinScale { get; init; } = 0.5;

    /// <summary>Maximum user-applied scale multiplier.</summary>
    public double MaxScale { get; init; } = 2.0;
}
