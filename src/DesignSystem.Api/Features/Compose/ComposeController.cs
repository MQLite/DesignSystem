using DesignSystem.Infrastructure.Persistence;
using DesignSystem.Infrastructure.Rendering;
using DesignSystem.Infrastructure.Rendering.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DesignSystem.Api.Features.Compose;

// ── Request / Response types ─────────────────────────────────────────────────

/// <summary>Caller supplies layout + optional subject; engine figures out canvas size.</summary>
public record ComposePreviewRequest(
    Guid BackgroundLayoutId,
    Guid? SubjectAssetId,
    string? TextConfigJson,
    /// <summary>Serialised CanvasLayout JSON from the frontend interactive editor.</summary>
    string? CanvasLayoutJson);

public record ComposePreviewResponse(
    string PreviewRelativePath,
    int WidthPx,
    int HeightPx);

public record ComposeExportResponse(
    string ExportRelativePath,
    int WidthPx,
    int HeightPx);

// ── Controller ───────────────────────────────────────────────────────────────

[ApiController]
[Route("api/compose")]
public sealed class ComposeController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IComposerEngine _engine;
    private readonly IWebHostEnvironment _env;

    public ComposeController(AppDbContext db, IComposerEngine engine, IWebHostEnvironment env)
    {
        _db = db;
        _engine = engine;
        _env = env;
    }

    /// <summary>
    /// Composes a preview image (150 DPI) for the given background layout + optional subject.
    /// Returns the relative path to the output file.
    /// </summary>
    [HttpPost("preview")]
    public async Task<ActionResult<ComposePreviewResponse>> Preview(
        [FromBody] ComposePreviewRequest request,
        CancellationToken ct)
    {
        // ── Load layout (includes parent Background for SourcePath) ──────────
        var layout = await _db.BackgroundLayouts
            .Include(l => l.Background)
            .FirstOrDefaultAsync(l => l.Id == request.BackgroundLayoutId, ct);

        if (layout is null)
            return NotFound($"BackgroundLayout {request.BackgroundLayoutId} not found.");

        if (layout.Background is null)
            return Problem("BackgroundLayout has no associated Background record.");

        // ── Optionally resolve subject cutout path ───────────────────────────
        string? subjectCutoutPath = null;
        if (request.SubjectAssetId.HasValue)
        {
            var asset = await _db.SubjectAssets
                .FirstOrDefaultAsync(a => a.Id == request.SubjectAssetId.Value, ct);

            // Prefer processed cutout; fall back to original upload
            subjectCutoutPath = asset?.CutoutPath ?? asset?.OriginalPath;
        }

        // ── Compute canvas pixel size from physical layout dims + 150 DPI ────
        const int previewDpi = 150;
        int canvasW = MmToPx(layout.WidthMm, previewDpi);
        int canvasH = MmToPx(layout.HeightMm, previewDpi);

        // ── Build ComposeRequest (no DB entities cross this boundary) ────────
        var storageRoot = Path.Combine(_env.ContentRootPath, "storage");

        var composeRequest = new ComposeRequest
        {
            BackgroundSourcePath = layout.Background.SourcePath,
            SubjectCutoutPath = subjectCutoutPath,
            SubjectSlotsJson = layout.SubjectSlotsJson,
            TextConfigJson = request.TextConfigJson ?? "{}",
            UserAdjustmentsJson = request.CanvasLayoutJson,
            TargetDpi = previewDpi,
            CanvasWidthPx = canvasW,
            CanvasHeightPx = canvasH,
            StorageRootPath = storageRoot,
        };

        // ── Invoke engine ────────────────────────────────────────────────────
        var result = await _engine.ComposePreviewAsync(composeRequest, ct);

        return Ok(new ComposePreviewResponse(result.OutputRelativePath, result.WidthPx, result.HeightPx));
    }

    /// <summary>
    /// Composes a print-ready SVG export (300 DPI equivalent) for the given layout.
    /// Returns the relative path to the output SVG file.
    /// </summary>
    [HttpPost("export/svg")]
    public async Task<ActionResult<ComposeExportResponse>> ExportSvg(
        [FromBody] ComposePreviewRequest request,
        CancellationToken ct)
    {
        var layout = await _db.BackgroundLayouts
            .Include(l => l.Background)
            .FirstOrDefaultAsync(l => l.Id == request.BackgroundLayoutId, ct);

        if (layout is null)
            return NotFound($"BackgroundLayout {request.BackgroundLayoutId} not found.");

        if (layout.Background is null)
            return Problem("BackgroundLayout has no associated Background record.");

        string? subjectCutoutPath = null;
        if (request.SubjectAssetId.HasValue)
        {
            var asset = await _db.SubjectAssets
                .FirstOrDefaultAsync(a => a.Id == request.SubjectAssetId.Value, ct);
            subjectCutoutPath = asset?.CutoutPath ?? asset?.OriginalPath;
        }

        const int exportDpi = 300;
        int canvasW = MmToPx(layout.WidthMm, exportDpi);
        int canvasH = MmToPx(layout.HeightMm, exportDpi);

        var storageRoot = Path.Combine(_env.ContentRootPath, "storage");

        var composeRequest = new ComposeRequest
        {
            BackgroundSourcePath = layout.Background.SourcePath,
            SubjectCutoutPath = subjectCutoutPath,
            SubjectSlotsJson = layout.SubjectSlotsJson,
            TextConfigJson = request.TextConfigJson ?? "{}",
            UserAdjustmentsJson = request.CanvasLayoutJson,
            TargetDpi = exportDpi,
            CanvasWidthPx = canvasW,
            CanvasHeightPx = canvasH,
            StorageRootPath = storageRoot,
        };

        var result = await _engine.ExportSvgAsync(composeRequest, ct);

        return Ok(new ComposeExportResponse(result.OutputRelativePath, result.WidthPx, result.HeightPx));
    }

    // A3 at 150 DPI = round(297 / 25.4 * 150) = 1754 px wide
    private static int MmToPx(int mm, int dpi) =>
        (int)Math.Round(mm / 25.4 * dpi);
}
