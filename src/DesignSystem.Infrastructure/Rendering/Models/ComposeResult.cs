namespace DesignSystem.Infrastructure.Rendering.Models;

/// <summary>Output produced by a successful compose operation.</summary>
public sealed record ComposeResult(
    /// <summary>Relative path to the output file, e.g. "storage/previews/abc123_preview.png".</summary>
    string OutputRelativePath,

    int WidthPx,
    int HeightPx,

    /// <summary>"preview-png" for preview; "export-png" / "export-pdf" for print export (future).</summary>
    string OutputType = "preview-png");
