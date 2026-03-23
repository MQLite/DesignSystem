namespace DesignSystem.Infrastructure.Rendering.Models;

/// <summary>
/// All inputs required by the composer engine to produce one output image.
/// Keep this record free of DB entities — callers load data from DB and map here.
/// </summary>
public sealed record ComposeRequest
{
    /// <summary>Relative path to the background source file, e.g. "storage/backgrounds/seeded/lily_source.png".</summary>
    public required string BackgroundSourcePath { get; init; }

    /// <summary>Relative path to the subject cutout (transparent PNG). Null if no subject yet.</summary>
    public string? SubjectCutoutPath { get; init; }

    /// <summary>Raw JSON from BackgroundLayout.SubjectSlotsJson.</summary>
    public required string SubjectSlotsJson { get; init; }

    /// <summary>Raw JSON from BackgroundLayout.TextZonesJson. Null when no text zones defined.</summary>
    public string? TextZonesJson { get; init; }

    /// <summary>Raw JSON text config (title, subtitle, footer). Use "{}" if empty.</summary>
    public required string TextConfigJson { get; init; }

    /// <summary>Output DPI. 150 for preview, 300 for print export.</summary>
    public int TargetDpi { get; init; } = 150;

    /// <summary>
    /// Canvas size in pixels — derived from layout physical dimensions + DPI by the caller.
    /// A3 Portrait at 150 DPI → 1754 × 2480. At 300 DPI → 3508 × 4961.
    /// </summary>
    public int CanvasWidthPx { get; init; }
    public int CanvasHeightPx { get; init; }

    /// <summary>Optional user-applied offset/scale adjustments (JSON). May be null.</summary>
    public string? UserAdjustmentsJson { get; init; }

    /// <summary>
    /// Serialised SubjectCropState[] from the frontend — user's pan/zoom within each crop frame.
    /// Null when the user has not adjusted the crop. Engine applies this before placing the subject.
    /// </summary>
    public string? SubjectCropStateJson { get; init; }

    /// <summary>
    /// Absolute path to the storage root (ContentRootPath + "storage").
    /// Provided by the caller (API layer) so the engine stays host-independent.
    /// </summary>
    public required string StorageRootPath { get; init; }
}
