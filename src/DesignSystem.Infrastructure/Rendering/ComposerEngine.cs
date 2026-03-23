using System.Text;
using System.Text.Json;
using DesignSystem.Infrastructure.Rendering.Helpers;
using DesignSystem.Infrastructure.Rendering.Models;
using Microsoft.Extensions.Logging;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace DesignSystem.Infrastructure.Rendering;

/// <summary>
/// Composer engine — produces rasterised PNG previews and SVG exports by:
///   1. Loading the background and resizing to canvas DPI dimensions.
///   2. Cover-fitting the subject photo into each slot with user crop pan/zoom applied.
///   3. Rendering text zones (PNG: ImageSharp text; SVG: &lt;text&gt; elements).
/// </summary>
public sealed class ComposerEngine : IComposerEngine
{
    private static readonly JsonSerializerOptions _jsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    private readonly ILogger<ComposerEngine> _logger;

    public ComposerEngine(ILogger<ComposerEngine> logger) => _logger = logger;

    /// <inheritdoc/>
    public async Task<ComposeResult> ComposePreviewAsync(
        ComposeRequest request,
        CancellationToken ct = default)
    {
        ValidateRequest(request);

        var slots      = SlotParser.Parse(request.SubjectSlotsJson);
        var cropStates = CropStateParser.Parse(request.SubjectCropStateJson);

        int cw = request.CanvasWidthPx;
        int ch = request.CanvasHeightPx;

        // ── 1. Load / create background canvas ───────────────────────────────
        var absBg = ResolveStoragePath(request.StorageRootPath, request.BackgroundSourcePath);
        using var canvas = await LoadOrCreateAsync(absBg, cw, ch, ct);

        // ── 2. Composite subject ──────────────────────────────────────────────
        if (request.SubjectCutoutPath is not null && slots.Count > 0)
        {
            var absSubject = ResolveStoragePath(request.StorageRootPath, request.SubjectCutoutPath);
            _logger.LogInformation("Subject path resolved: {Path} (exists={Exists})", absSubject, File.Exists(absSubject));
            if (File.Exists(absSubject))
            {
                var slot      = slots[0];
                var cropState = CropStateParser.GetOrDefault(cropStates, slot.Id);
                var (cropped, dstX, dstY) = await CropSubjectImageAsync(absSubject, slot, cropState, cw, ch, ct);
                if (cropped is not null)
                    using (cropped)
                        canvas.Mutate(ctx => ctx.DrawImage(cropped, new Point(dstX, dstY), 1f));
            }
            else
            {
                _logger.LogWarning("Subject file not found: {Path}", absSubject);
            }
        }

        // ── 3. Render text zones ──────────────────────────────────────────────
        RenderTextZones(canvas, request.TextZonesJson, request.TextConfigJson, cw, ch);

        // ── 4. Save PNG ───────────────────────────────────────────────────────
        var previewDir    = Path.Combine(request.StorageRootPath, "previews");
        Directory.CreateDirectory(previewDir);

        var fileName      = $"{Guid.NewGuid():N}_preview.png";
        var absOutputPath = Path.Combine(previewDir, fileName);
        await canvas.SaveAsPngAsync(absOutputPath, new PngEncoder(), ct);

        _logger.LogInformation("Preview written → {RelPath}", $"storage/previews/{fileName}");

        return new ComposeResult(
            OutputRelativePath: $"storage/previews/{fileName}",
            WidthPx: cw,
            HeightPx: ch,
            OutputType: "preview-png");
    }

    /// <inheritdoc/>
    public async Task<ComposeResult> ExportSvgAsync(
        ComposeRequest request,
        CancellationToken ct = default)
    {
        ValidateRequest(request);

        var slots      = SlotParser.Parse(request.SubjectSlotsJson);
        var cropStates = CropStateParser.Parse(request.SubjectCropStateJson);

        int cw = request.CanvasWidthPx;
        int ch = request.CanvasHeightPx;

        var svg = new StringBuilder();
        svg.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        svg.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{cw}" height="{ch}" viewBox="0 0 {cw} {ch}">""");

        // ── 1. Background ─────────────────────────────────────────────────────
        var absBg  = ResolveStoragePath(request.StorageRootPath, request.BackgroundSourcePath);
        var bgUri  = await LoadResizeToDataUriAsync(absBg, cw, ch, ResizeMode.Stretch, ct);
        svg.AppendLine($"""  <image x="0" y="0" width="{cw}" height="{ch}" preserveAspectRatio="none" href="{bgUri}"/>""");

        // ── 2. Subject ────────────────────────────────────────────────────────
        if (request.SubjectCutoutPath is not null && slots.Count > 0)
        {
            var absSubject = ResolveStoragePath(request.StorageRootPath, request.SubjectCutoutPath);
            if (File.Exists(absSubject))
            {
                var slot      = slots[0];
                var cropState = CropStateParser.GetOrDefault(cropStates, slot.Id);
                var (cropped, dstX, dstY) = await CropSubjectImageAsync(absSubject, slot, cropState, cw, ch, ct);
                if (cropped is not null)
                {
                    int drawW = cropped.Width;
                    int drawH = cropped.Height;
                    using (cropped)
                    {
                        var subUri = await ToDataUriAsync(cropped, ct);
                        svg.AppendLine($"""  <image x="{dstX}" y="{dstY}" width="{drawW}" height="{drawH}" href="{subUri}"/>""");
                    }
                }
            }
            else
            {
                _logger.LogWarning("Subject file not found for SVG export: {Path}", absSubject);
            }
        }

        // ── 3. Text zones ─────────────────────────────────────────────────────
        AppendSvgTextZones(svg, request.TextZonesJson, request.TextConfigJson, cw, ch);

        svg.AppendLine("</svg>");

        // ── 4. Save SVG ───────────────────────────────────────────────────────
        var exportDir     = Path.Combine(request.StorageRootPath, "exports");
        Directory.CreateDirectory(exportDir);

        var fileName      = $"{Guid.NewGuid():N}_export.svg";
        var absOutputPath = Path.Combine(exportDir, fileName);
        var relOutputPath = $"storage/exports/{fileName}";

        await File.WriteAllTextAsync(absOutputPath, svg.ToString(), ct);
        _logger.LogInformation("SVG export written → {RelPath}", relOutputPath);

        return new ComposeResult(
            OutputRelativePath: relOutputPath,
            WidthPx: cw,
            HeightPx: ch,
            OutputType: "export-svg");
    }

    // ── Shared crop pipeline ──────────────────────────────────────────────────

    /// <summary>
    /// Loads the subject image and applies the crop-window model, returning the cropped
    /// and resized image ready to composite. The <b>caller must dispose</b> the returned image.
    /// Returns (null, 0, 0) when the crop region is degenerate.
    /// </summary>
    private async Task<(Image<Rgba32>? Cropped, int DstX, int DstY)> CropSubjectImageAsync(
        string absSubjectPath,
        SubjectSlot slot,
        CropStateEntry cropState,
        int cw, int ch,
        CancellationToken ct)
    {
        var subject = await Image.LoadAsync<Rgba32>(absSubjectPath, ct);
        try
        {
            var (cropX, cropY, cropW, cropH) = LayoutCalculator.ToPixels(slot.Rect, cw, ch);
            cropW = Math.Max(1, cropW);
            cropH = Math.Max(1, cropH);

            int srcW = subject.Width;
            int srcH = subject.Height;

            // ── Derive crop window in source-image space ──────────────────────
            //
            // coverScale = scale that makes the source just cover the slot.
            // finalScale = coverScale × user-scale.
            // imgLeft/Top = top-left of the scaled source within the slot viewport.
            // Projecting the slot back into source space gives the crop window.
            //
            double coverScale = Math.Max((double)cropW / srcW, (double)cropH / srcH);
            double finalScale = coverScale * Math.Max(0.01, cropState.Scale);

            double panX = cropState.OffsetX * cropW;
            double panY = cropState.OffsetY * cropH;

            double imgLeft = (cropW - srcW * finalScale) / 2.0 + panX;
            double imgTop  = (cropH - srcH * finalScale) / 2.0 + panY;

            double wX = -imgLeft / finalScale;
            double wY = -imgTop  / finalScale;
            double wW =  cropW   / finalScale;
            double wH =  cropH   / finalScale;

            // ── Clamp crop window to source bounds ────────────────────────────
            double cX = Math.Max(0.0, wX);
            double cY = Math.Max(0.0, wY);
            double cW = Math.Min(wW - (cX - wX), srcW - cX);
            double cH = Math.Min(wH - (cY - wY), srcH - cY);

            if (cW < 1 || cH < 1) { subject.Dispose(); return (null, 0, 0); }

            int iCX = (int)Math.Round(cX);
            int iCY = (int)Math.Round(cY);
            int iCW = Math.Max(1, (int)Math.Round(cW));
            int iCH = Math.Max(1, (int)Math.Round(cH));
            iCW = Math.Min(iCW, srcW - iCX);
            iCH = Math.Min(iCH, srcH - iCY);
            if (iCW < 1 || iCH < 1) { subject.Dispose(); return (null, 0, 0); }

            subject.Mutate(ctx => ctx.Crop(new Rectangle(iCX, iCY, iCW, iCH)));

            // ── Scale cropped region to fill slot ─────────────────────────────
            //
            // Fully contained → aspect ratios match → Stretch fills slot exactly.
            // Clamped (scale < 1 or excessive pan) → letterbox to avoid distortion.
            //
            bool fullyContained = cX <= wX + 0.5 && cY <= wY + 0.5 &&
                                  cX + cW >= wX + wW - 0.5 &&
                                  cY + cH >= wY + wH - 0.5;

            subject.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(cropW, cropH),
                Mode = fullyContained ? ResizeMode.Stretch : ResizeMode.Max,
            }));

            int dstX = cropX + (cropW - subject.Width)  / 2;
            int dstY = cropY + (cropH - subject.Height) / 2;

            _logger.LogInformation(
                "Subject cropped — slot({CX},{CY},{CW},{CH}) src({SW}×{SH}) " +
                "scale={Scale:F2} cropWin=({WX:F1},{WY:F1},{WW:F1}×{WH:F1}) " +
                "clamped=({IX},{IY},{IW}×{IH}) dst=({DX},{DY}) mode={Mode}",
                cropX, cropY, cropW, cropH, srcW, srcH,
                cropState.Scale, wX, wY, wW, wH,
                iCX, iCY, iCW, iCH, dstX, dstY,
                fullyContained ? "Stretch" : "Letterbox");

            return (subject, dstX, dstY);
        }
        catch
        {
            subject.Dispose();
            throw;
        }
    }

    // ── PNG helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the background image and resizes (stretch) it to the canvas pixel dimensions.
    /// Falls back to a solid grey canvas when the file is missing.
    /// </summary>
    private async Task<Image<Rgba32>> LoadOrCreateAsync(
        string absPath, int cw, int ch, CancellationToken ct)
    {
        if (!File.Exists(absPath))
        {
            _logger.LogWarning("Background not found: {Path} — using grey fallback.", absPath);
            return new Image<Rgba32>(cw, ch, new Rgba32(220, 220, 220));
        }

        var img = await Image.LoadAsync<Rgba32>(absPath, ct);
        img.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(cw, ch),
            Mode = ResizeMode.Stretch,
        }));
        return img;
    }

    /// <summary>
    /// Renders title / subtitle / footer text at the zones defined in TextZonesJson.
    /// Silently skips rendering when fonts or zone data are unavailable.
    /// </summary>
    private void RenderTextZones(
        Image<Rgba32> canvas,
        string? textZonesJson,
        string? textConfigJson,
        int cw, int ch)
    {
        if (string.IsNullOrWhiteSpace(textZonesJson) ||
            string.IsNullOrWhiteSpace(textConfigJson))
            return;

        var textValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = JsonDocument.Parse(textConfigJson);
            foreach (var prop in doc.RootElement.EnumerateObject())
                textValues[prop.Name] = prop.Value.GetString() ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse TextConfigJson — skipping text rendering.");
            return;
        }

        TextZoneDto[]? zones;
        try
        {
            zones = JsonSerializer.Deserialize<TextZoneDto[]>(textZonesJson, _jsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse TextZonesJson — skipping text rendering.");
            return;
        }
        if (zones is null || zones.Length == 0) return;

        FontFamily? fontFamily = null;
        foreach (var name in new[] { "Arial", "Helvetica", "DejaVu Sans", "Liberation Sans", "Segoe UI", "Tahoma" })
        {
            if (SystemFonts.TryGet(name, out var ff)) { fontFamily = ff; break; }
        }
        if (fontFamily is null)
        {
            _logger.LogWarning("No suitable system font found — skipping text rendering.");
            return;
        }

        canvas.Mutate(ctx =>
        {
            foreach (var zone in zones)
            {
                textValues.TryGetValue(zone.Id, out var text);
                text ??= "";
                if (string.IsNullOrWhiteSpace(text)) continue;

                int zx = (int)Math.Round(zone.X * cw);
                int zy = (int)Math.Round(zone.Y * ch);
                int zw = Math.Max(1, (int)Math.Round(zone.W * cw));
                int zh = Math.Max(1, (int)Math.Round(zone.H * ch));

                float fontSize = Math.Max(8f, zh * 0.60f);
                var font = fontFamily.Value.CreateFont(fontSize,
                    zone.Id == "title" ? FontStyle.Bold : FontStyle.Regular);

                var center = new PointF(zx + zw / 2f, zy + zh / 2f);

                ctx.DrawText(
                    new RichTextOptions(font)
                    {
                        Origin              = new PointF(center.X + 2, center.Y + 2),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment   = VerticalAlignment.Center,
                        WrappingLength      = zw,
                    },
                    text,
                    Color.FromRgba(0, 0, 0, 180));

                ctx.DrawText(
                    new RichTextOptions(font)
                    {
                        Origin              = center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment   = VerticalAlignment.Center,
                        WrappingLength      = zw,
                    },
                    text,
                    Color.White);
            }
        });
    }

    // ── SVG helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Loads an image file, resizes it, and returns a PNG data URI (base64).
    /// Falls back to a grey rectangle when the file is missing.
    /// </summary>
    private static async Task<string> LoadResizeToDataUriAsync(
        string absPath, int targetW, int targetH, ResizeMode mode, CancellationToken ct)
    {
        Image<Rgba32> img;
        if (File.Exists(absPath))
        {
            img = await Image.LoadAsync<Rgba32>(absPath, ct);
            img.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(targetW, targetH),
                Mode = mode,
            }));
        }
        else
        {
            img = new Image<Rgba32>(targetW, targetH, new Rgba32(220, 220, 220));
        }

        using (img)
            return await ToDataUriAsync(img, ct);
    }

    /// <summary>Encodes an ImageSharp image as a PNG data URI (base64).</summary>
    private static async Task<string> ToDataUriAsync(Image<Rgba32> img, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await img.SaveAsPngAsync(ms, new PngEncoder(), ct);
        return $"data:image/png;base64,{Convert.ToBase64String(ms.ToArray())}";
    }

    /// <summary>
    /// Appends SVG &lt;text&gt; elements for each defined text zone.
    /// Each zone gets a semi-transparent drop-shadow and a white foreground text element.
    /// </summary>
    private static void AppendSvgTextZones(
        StringBuilder svg,
        string? textZonesJson,
        string? textConfigJson,
        int cw, int ch)
    {
        if (string.IsNullOrWhiteSpace(textZonesJson) || string.IsNullOrWhiteSpace(textConfigJson))
            return;

        var textValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = JsonDocument.Parse(textConfigJson);
            foreach (var prop in doc.RootElement.EnumerateObject())
                textValues[prop.Name] = prop.Value.GetString() ?? "";
        }
        catch { return; }

        TextZoneDto[]? zones;
        try
        {
            zones = JsonSerializer.Deserialize<TextZoneDto[]>(textZonesJson, _jsonOpts);
        }
        catch { return; }
        if (zones is null || zones.Length == 0) return;

        foreach (var zone in zones)
        {
            textValues.TryGetValue(zone.Id, out var text);
            if (string.IsNullOrWhiteSpace(text)) continue;

            int zx = (int)Math.Round(zone.X * cw);
            int zy = (int)Math.Round(zone.Y * ch);
            int zw = Math.Max(1, (int)Math.Round(zone.W * cw));
            int zh = Math.Max(1, (int)Math.Round(zone.H * ch));

            float fontSize  = Math.Max(8f, zh * 0.60f);
            string weight   = zone.Id == "title" ? "bold" : "normal";
            string fontAttr = $"font-family=\"Arial, Helvetica, sans-serif\" font-size=\"{fontSize:F0}\" font-weight=\"{weight}\" text-anchor=\"middle\" dominant-baseline=\"middle\"";

            int cx = zx + zw / 2;
            int cy = zy + zh / 2;

            // XML-escape text content
            var escaped = text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");

            // Drop shadow
            svg.AppendLine($"""  <text x="{cx + 2}" y="{cy + 2}" {fontAttr} fill="#000000" fill-opacity="0.7">{escaped}</text>""");
            // Foreground
            svg.AppendLine($"""  <text x="{cx}" y="{cy}" {fontAttr} fill="#ffffff">{escaped}</text>""");
        }
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private static void ValidateRequest(ComposeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BackgroundSourcePath))
            throw new ArgumentException("BackgroundSourcePath is required.", nameof(request));

        if (request.CanvasWidthPx <= 0 || request.CanvasHeightPx <= 0)
            throw new ArgumentException(
                $"Canvas dimensions must be positive (got {request.CanvasWidthPx}×{request.CanvasHeightPx}).",
                nameof(request));

        if (string.IsNullOrWhiteSpace(request.StorageRootPath))
            throw new ArgumentException("StorageRootPath is required.", nameof(request));
    }

    /// <summary>
    /// Resolves a relative storage path (e.g. "storage/uploads/file.jpg") against the
    /// absolute StorageRootPath by stripping the leading "storage/" prefix.
    /// </summary>
    private static string ResolveStoragePath(string storageRoot, string relativePath)
    {
        const string prefix = "storage/";
        var suffix = relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? relativePath[prefix.Length..]
            : relativePath;
        return Path.GetFullPath(Path.Combine(storageRoot, suffix));
    }

    // ── DTOs used only within this file ──────────────────────────────────────

    private sealed record TextZoneDto(
        string Id,
        double X,
        double Y,
        double W,
        double H);
}
