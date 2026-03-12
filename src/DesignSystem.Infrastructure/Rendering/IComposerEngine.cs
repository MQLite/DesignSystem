using DesignSystem.Infrastructure.Rendering.Models;

namespace DesignSystem.Infrastructure.Rendering;

/// <summary>
/// Composes a layered design image from background, subject cutout, and text config.
/// Implementations must be stateless — all inputs come through <see cref="ComposeRequest"/>.
/// </summary>
public interface IComposerEngine
{
    /// <summary>
    /// Produces a preview PNG at 150 DPI and writes it to the storage/previews folder.
    /// </summary>
    Task<ComposeResult> ComposePreviewAsync(ComposeRequest request, CancellationToken ct = default);

    /// <summary>
    /// Produces a print-ready SVG export and writes it to the storage/exports folder.
    /// </summary>
    Task<ComposeResult> ExportSvgAsync(ComposeRequest request, CancellationToken ct = default);
}
