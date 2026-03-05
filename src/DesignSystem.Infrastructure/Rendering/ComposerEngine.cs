using DesignSystem.Infrastructure.Rendering.Helpers;
using DesignSystem.Infrastructure.Rendering.Models;
using Microsoft.Extensions.Logging;

namespace DesignSystem.Infrastructure.Rendering;

/// <summary>
/// Skeleton implementation of <see cref="IComposerEngine"/>.
/// Real image rendering is marked with TODO — replace with ImageSharp calls.
/// </summary>
public sealed class ComposerEngine : IComposerEngine
{
    private readonly ILogger<ComposerEngine> _logger;

    public ComposerEngine(ILogger<ComposerEngine> logger) => _logger = logger;

    /// <inheritdoc/>
    public async Task<ComposeResult> ComposePreviewAsync(
        ComposeRequest request,
        CancellationToken ct = default)
    {
        // ── 1. Validate inputs ───────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(request.BackgroundSourcePath))
            throw new ArgumentException("BackgroundSourcePath is required.", nameof(request));

        if (request.CanvasWidthPx <= 0 || request.CanvasHeightPx <= 0)
            throw new ArgumentException(
                $"Canvas dimensions must be positive (got {request.CanvasWidthPx}×{request.CanvasHeightPx}).",
                nameof(request));

        if (string.IsNullOrWhiteSpace(request.StorageRootPath))
            throw new ArgumentException("StorageRootPath is required.", nameof(request));

        // ── 2. Parse subject slots ───────────────────────────────────────────
        var slots = SlotParser.Parse(request.SubjectSlotsJson);
        _logger.LogDebug("Parsed {Count} subject slot(s) from layout.", slots.Count);

        // ── 3. Compute subject placement (no drawing yet) ────────────────────
        if (slots.Count > 0 && request.SubjectCutoutPath is not null)
        {
            // TODO (ImageSharp): load actual subject image dimensions:
            //   var absSubject = Path.Combine(request.StorageRootPath, "..", request.SubjectCutoutPath);
            //   using var subjectImg = await Image.LoadAsync<Rgba32>(absSubject, ct);
            //   int srcW = subjectImg.Width;
            //   int srcH = subjectImg.Height;

            // Skeleton: use placeholder dimensions until real image loading is implemented
            const int placeholderW = 800;
            const int placeholderH = 1200;

            var placement = LayoutCalculator.CalculatePlacement(
                placeholderW, placeholderH,
                slots[0],
                request.CanvasWidthPx, request.CanvasHeightPx);

            _logger.LogDebug(
                "Computed slot[0] placement — X:{X} Y:{Y} W:{W} H:{H}",
                placement.X, placement.Y, placement.W, placement.H);
        }

        // ── 4. Ensure output directory exists ────────────────────────────────
        var previewDir = Path.Combine(request.StorageRootPath, "previews");
        Directory.CreateDirectory(previewDir);

        // ── 5. Build output path ─────────────────────────────────────────────
        var fileName = $"{Guid.NewGuid():N}_preview.png";
        var absOutputPath = Path.Combine(previewDir, fileName);
        var relOutputPath = $"storage/previews/{fileName}";

        // ── 6. Skeleton placeholder ──────────────────────────────────────────
        // TODO (ImageSharp full implementation):
        //   Step A — Load background:
        //     var absBg = Path.Combine(request.StorageRootPath, "..", request.BackgroundSourcePath);
        //     using var canvas = await Image.LoadAsync<Rgba32>(absBg, ct);
        //     canvas.Mutate(ctx => ctx.Resize(request.CanvasWidthPx, request.CanvasHeightPx));
        //
        //   Step B — Composite subject (if present):
        //     var absSubject = ...;
        //     using var subjectImg = await Image.LoadAsync<Rgba32>(absSubject, ct);
        //     subjectImg.Mutate(ctx => ctx.Resize(placement.W, placement.H));
        //     canvas.Mutate(ctx => ctx.DrawImage(subjectImg, new Point(placement.X, placement.Y), opacity: 1f));
        //
        //   Step C — Render text zones:
        //     Parse TextConfigJson, load font, call canvas.Mutate(ctx => ctx.DrawText(...));
        //
        //   Step D — Save:
        //     await canvas.SaveAsPngAsync(absOutputPath, ct);

        await File.WriteAllTextAsync(
            absOutputPath,
            $"[ComposerEngine SKELETON]\n" +
            $"bg={request.BackgroundSourcePath}\n" +
            $"subject={request.SubjectCutoutPath ?? "(none)"}\n" +
            $"dpi={request.TargetDpi}  canvas={request.CanvasWidthPx}x{request.CanvasHeightPx}\n" +
            $"generated={DateTimeOffset.UtcNow:O}",
            ct);

        _logger.LogInformation(
            "Skeleton preview placeholder written → {RelPath}", relOutputPath);

        return new ComposeResult(
            OutputRelativePath: relOutputPath,
            WidthPx: request.CanvasWidthPx,
            HeightPx: request.CanvasHeightPx,
            OutputType: "preview-png");
    }
}
