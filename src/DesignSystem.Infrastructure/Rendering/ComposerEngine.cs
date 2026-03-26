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
        var bgResult = await LoadBackgroundWithCropAsync(absBg, cw, ch, BgCropEntry.Parse(request.BgCropJson), ct, logDetails: true);
        using var canvas = bgResult.Canvas;

        // ── 2. Composite subject ──────────────────────────────────────────────
        if (request.SubjectCutoutPath is not null && slots.Count > 0)
        {
            var absSubject = ResolveStoragePath(request.StorageRootPath, request.SubjectCutoutPath);
            _logger.LogInformation("Subject path resolved: {Path} (exists={Exists})", absSubject, File.Exists(absSubject));
            if (File.Exists(absSubject))
            {
                var slot      = slots[0];
                var cropState = CropStateParser.GetOrDefault(cropStates, slot.Id);
                var (cropped, dstX, dstY) = await CropSubjectImageAsync(absSubject, slot, cropState, cw, ch, ct, logDetails: true);
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
        RenderTextZones(canvas, request.TextZonesJson, request.TextConfigJson, request.TextStyleOverridesJson, cw, ch);

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

        // Use a placeholder so we can back-fill the tight viewBox after measuring content.
        const string vbPlaceholder = "%%VIEWBOX%%";
        var svg = new StringBuilder();
        svg.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="no"?>""");
        svg.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" version="1.1" viewBox="{vbPlaceholder}">""");

        // Track content bounding box across all layers.
        int minX = cw, minY = ch, maxX = 0, maxY = 0;
        void Expand(int x, int y, int w, int h)
        {
            if (w <= 0 || h <= 0) return;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x + w);
            maxY = Math.Max(maxY, y + h);
        }

        // ── 1. Background ─────────────────────────────────────────────────────
        var absBg = ResolveStoragePath(request.StorageRootPath, request.BackgroundSourcePath);
        var (bgCanvas, bgRect) = await LoadBackgroundWithCropAsync(absBg, cw, ch, BgCropEntry.Parse(request.BgCropJson), ct, transparentFill: true);
        using (bgCanvas)
        {
            var bgUri = await ToDataUriAsync(bgCanvas, ct);
            // Embed only the content rect to avoid encoding transparent padding.
            svg.AppendLine($"""  <image x="{bgRect.X}" y="{bgRect.Y}" width="{bgRect.Width}" height="{bgRect.Height}" preserveAspectRatio="none" xlink:href="{bgUri}"/>""");
        }
        Expand(bgRect.X, bgRect.Y, bgRect.Width, bgRect.Height);

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
                        const string clipId = "slot_clip_0";
                        var clipPathSvg = BuildSvgClipPath(clipId, slot, cw, ch, dstX, dstY, drawW, drawH);
                        if (clipPathSvg is not null)
                            svg.AppendLine($"""  <defs>{clipPathSvg}</defs>""");
                        var clipAttr = clipPathSvg is not null ? $""" clip-path="url(#{clipId})" """ : " ";
                        svg.AppendLine($"""  <image x="{dstX}" y="{dstY}" width="{drawW}" height="{drawH}"{clipAttr}xlink:href="{subUri}"/>""");
                    }
                    Expand(dstX, dstY, drawW, drawH);
                }
            }
            else
            {
                _logger.LogWarning("Subject file not found for SVG export: {Path}", absSubject);
            }
        }

        // ── 3. Text zones ─────────────────────────────────────────────────────
        AppendSvgTextZones(svg, request.TextZonesJson, request.TextConfigJson, request.TextStyleOverridesJson, cw, ch);

        svg.AppendLine("</svg>");

        // ── 4. Compute tight viewBox and save ─────────────────────────────────
        // Fall back to full canvas if no content was measured.
        if (maxX <= minX || maxY <= minY) { minX = 0; minY = 0; maxX = cw; maxY = ch; }
        var viewBox   = $"{minX} {minY} {maxX - minX} {maxY - minY}";
        var svgOutput = svg.ToString().Replace(vbPlaceholder, viewBox);

        var exportDir     = Path.Combine(request.StorageRootPath, "exports");
        Directory.CreateDirectory(exportDir);

        var fileName      = $"{Guid.NewGuid():N}_export.svg";
        var absOutputPath = Path.Combine(exportDir, fileName);
        var relOutputPath = $"storage/exports/{fileName}";

        await File.WriteAllTextAsync(absOutputPath, svgOutput, ct);
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
        CancellationToken ct,
        bool logDetails = false)
    {
        using var subject = await Image.LoadAsync<Rgba32>(absSubjectPath, ct);

        // Apply EXIF orientation so the backend matches browser auto-orientation
        subject.Mutate(ctx => ctx.AutoOrient());

        var (cropX, cropY, cropW, cropH) = LayoutCalculator.ToPixels(slot.Rect, cw, ch);
        cropW = Math.Max(1, cropW);
        cropH = Math.Max(1, cropH);

        int srcW = subject.Width;
        int srcH = subject.Height;

        // ── Step 1: Scale ─────────────────────────────────────────────────────
        //
        // containScale = maximum scale so the source fits entirely within the slot
        //   (same as CSS `max-width: 100%; max-height: 100%` on the subject <img>).
        // At scale=1.0 the full cutout is visible; scale>1 zooms in (edges clip);
        // scale<1 shrinks further — remaining slot area stays transparent.
        //
        double containScale = Math.Min((double)cropW / srcW, (double)cropH / srcH);
        double finalScale = containScale * Math.Max(0.01, cropState.Scale);

        int scaledW = Math.Max(1, (int)Math.Round(srcW * finalScale));
        int scaledH = Math.Max(1, (int)Math.Round(srcH * finalScale));
        subject.Mutate(ctx => ctx.Resize(scaledW, scaledH));

        // ── Step 2: Pan ───────────────────────────────────────────────────────
        //
        // panX/panY in slot pixels — mirrors CSS `translate(panX px, panY px)`.
        // The scaled image is centred in the slot, then offset by the user pan.
        //
        double panX = cropState.OffsetX * cropW;
        double panY = cropState.OffsetY * cropH;

        // Top-left corner of the scaled image within the slot viewport
        int imgLeft = (int)Math.Round((cropW - scaledW) / 2.0 + panX);
        int imgTop  = (int)Math.Round((cropH - scaledH) / 2.0 + panY);

        // ── Step 3: Clip to slot viewport ─────────────────────────────────────
        //
        // Determine the rectangle in the scaled image that overlaps the slot.
        //
        int clipX = Math.Max(0, -imgLeft);
        int clipY = Math.Max(0, -imgTop);
        int clipW = Math.Min(scaledW - clipX, cropW - Math.Max(0, imgLeft));
        int clipH = Math.Min(scaledH - clipY, cropH - Math.Max(0, imgTop));

        if (clipW <= 0 || clipH <= 0)
            return (null, 0, 0);

        subject.Mutate(ctx => ctx.Crop(new Rectangle(clipX, clipY, clipW, clipH)));

        // ── Step 4: Composite into full slot-sized transparent canvas ─────────
        //
        // Always produce a cropW × cropH image so:
        //   (a) the shape mask is applied over the full slot dimensions, and
        //   (b) unfilled areas (extreme pan / zoom-out) are transparent pixels
        //       rather than leaking background colour.
        //
        var slotCanvas = new Image<Rgba32>(cropW, cropH, Color.Transparent);
        int pasteX = Math.Max(0, imgLeft);
        int pasteY = Math.Max(0, imgTop);
        slotCanvas.Mutate(ctx => ctx.DrawImage(subject, new Point(pasteX, pasteY), 1f));
        // subject is disposed by `using` at end of method

        // Apply non-rectangular shape mask (ellipse / polygon)
        ApplyShapeMask(slotCanvas, slot, cw, ch, cropX, cropY);

        if (logDetails)
            _logger.LogInformation(
                "Subject placed — slot({CX},{CY},{CW},{CH}) src({SW}×{SH}) " +
                "containScale={Contain:F3} userScale={User:F2} finalScale={Final:F3} " +
                "scaled=({ScW}×{ScH}) pan=({PX:F1},{PY:F1}) " +
                "imgTopLeft=({IL},{IT}) clip=({ClX},{ClY},{ClW}×{ClH}) dst=({DX},{DY})",
                cropX, cropY, cropW, cropH, srcW, srcH,
                containScale, cropState.Scale, finalScale,
                scaledW, scaledH, panX, panY,
                imgLeft, imgTop, clipX, clipY, clipW, clipH, cropX, cropY);

        return (slotCanvas, cropX, cropY);
    }

    // ── PNG helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the background image, applies the admin-defined bgCrop transform
    /// (cover-scale × user scale + offset), and returns a canvas-sized image.
    /// Falls back to a solid grey canvas when the file is missing.
    /// </summary>
    private async Task<(Image<Rgba32> Canvas, Rectangle ContentRect)> LoadBackgroundWithCropAsync(
        string absPath, int cw, int ch, BgCropEntry crop, CancellationToken ct,
        bool logDetails = false, bool transparentFill = false)
    {
        var fillColor = transparentFill ? Color.Transparent.ToPixel<Rgba32>() : new Rgba32(220, 220, 220);
        if (!File.Exists(absPath))
        {
            _logger.LogWarning("Background not found: {Path} — using grey fallback.", absPath);
            return (new Image<Rgba32>(cw, ch, fillColor), new Rectangle(0, 0, cw, ch));
        }

        var src = await Image.LoadAsync<Rgba32>(absPath, ct);
        try
        {
            // Capture original dimensions before resize for accurate logging
            int origSrcW = src.Width;
            int origSrcH = src.Height;

            // Contain scale: image fits entirely within canvas — matches CSS object-contain used
            // in CropEditor and DesignCanvas so Admin bgCrop offsets map correctly.
            // bgCrop.Scale=1.0 → exact contain; >1 zooms in further.
            double containScale = Math.Min((double)cw / origSrcW, (double)ch / origSrcH);
            double finalScale = containScale * Math.Max(0.01, crop.Scale);

            int scaledW = Math.Max(1, (int)Math.Round(origSrcW * finalScale));
            int scaledH = Math.Max(1, (int)Math.Round(origSrcH * finalScale));

            src.Mutate(ctx => ctx.Resize(scaledW, scaledH));

            // Image top-left on canvas (centred + user offset)
            int imgX = (int)Math.Round((cw - scaledW) / 2.0 + crop.OffsetX * cw);
            int imgY = (int)Math.Round((ch - scaledH) / 2.0 + crop.OffsetY * ch);

            // Visible crop window within the scaled image
            int srcX = Math.Max(0, -imgX);
            int srcY = Math.Max(0, -imgY);
            int dstX = Math.Max(0,  imgX);
            int dstY = Math.Max(0,  imgY);
            int visW = Math.Min(scaledW - srcX, cw - dstX);
            int visH = Math.Min(scaledH - srcY, ch - dstY);

            if (logDetails)
                _logger.LogInformation(
                    "Background placed — canvas({CW}×{CH}) src({SW}×{SH}) " +
                    "coverScale={Cover:F3} bgCropScale={BgS:F3} finalScale={Final:F3} " +
                    "scaled=({ScW}×{ScH}) offset=({OX:F3},{OY:F3}) " +
                    "imgTopLeft=({IX},{IY}) vis=({SX},{SY},{VW}×{VH}) dst=({DX},{DY})",
                    cw, ch, origSrcW, origSrcH,
                    containScale, crop.Scale, finalScale,
                    scaledW, scaledH, crop.OffsetX, crop.OffsetY,
                    imgX, imgY, srcX, srcY, visW, visH, dstX, dstY);

            var canvas = new Image<Rgba32>(cw, ch, fillColor);
            if (visW > 0 && visH > 0)
            {
                using var cropped = src.Clone(ctx => ctx.Crop(new Rectangle(srcX, srcY, visW, visH)));
                canvas.Mutate(ctx => ctx.DrawImage(cropped, new Point(dstX, dstY), 1f));
            }
            var contentRect = visW > 0 && visH > 0
                ? new Rectangle(dstX, dstY, visW, visH)
                : new Rectangle(0, 0, cw, ch);
            return (canvas, contentRect);
        }
        finally
        {
            src.Dispose();
        }
    }

    /// <summary>
    /// Renders text at the zones defined in TextZonesJson, applying per-zone typography
    /// from the zone defaults merged with optional user overrides.
    /// </summary>
    private void RenderTextZones(
        Image<Rgba32> canvas,
        string? textZonesJson,
        string? textConfigJson,
        string? textStyleOverridesJson,
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
        try { zones = JsonSerializer.Deserialize<TextZoneDto[]>(textZonesJson, _jsonOpts); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse TextZonesJson — skipping text rendering.");
            return;
        }
        if (zones is null || zones.Length == 0) return;

        var overrides = ParseStyleOverrides(textStyleOverridesJson);

        foreach (var zone in zones)
        {
            textValues.TryGetValue(zone.Id, out var text);
            text = string.IsNullOrWhiteSpace(text) ? zone.DefaultText ?? "" : text;
            if (string.IsNullOrWhiteSpace(text)) continue;

            var ov = overrides.GetValueOrDefault(zone.Id);

            int zx = (int)Math.Round(zone.X * cw);
            int zy = (int)Math.Round(zone.Y * ch);
            int zw = Math.Max(1, (int)Math.Round(zone.W * cw));
            int zh = Math.Max(1, (int)Math.Round(zone.H * ch));

            // Resolve effective style (zone default → user override)
            double effectiveFontSize = ov?.FontSize    ?? zone.FontSize;
            string effectiveFontName = ov?.FontFamily  ?? zone.FontFamily;
            string effectiveColor    = ov?.Color       ?? zone.Color;
            double effectiveStrokeW  = ov?.StrokeWidth ?? zone.StrokeWidth;
            string effectiveStrokeC  = ov?.StrokeColor ?? zone.StrokeColor;
            string effectiveAlign    = ov?.Align       ?? zone.Align;

            // Resolve font
            FontFamily? ff = null;
            foreach (var name in new[] { effectiveFontName, "Arial", "Helvetica", "DejaVu Sans", "Liberation Sans", "Segoe UI", "Tahoma" })
            {
                if (!string.IsNullOrWhiteSpace(name) && SystemFonts.TryGet(name, out var found))
                { ff = found; break; }
            }
            if (ff is null) { _logger.LogWarning("No system font found — skipping zone {Id}.", zone.Id); continue; }

            float fSize = Math.Max(8f, (float)(zh * effectiveFontSize / 100.0));
            var font = ff.Value.CreateFont(fSize,
                zone.Id == "title" ? FontStyle.Bold : FontStyle.Regular);

            var fillColor   = ParseColor(effectiveColor,   Color.White);
            var strokeColor = ParseColor(effectiveStrokeC, Color.Black);
            float strokePx  = effectiveStrokeW > 0
                ? Math.Max(0.5f, (float)(fSize * effectiveStrokeW / 100.0)) : 0f;

            // ── Arc text ──────────────────────────────────────────────────────
            if (zone.ArcEnabled)
            {
                RenderArcText(canvas, text, font, fillColor, strokeColor, strokePx,
                    zx, zy, zw, zh, zone.ArcRx, zone.ArcRy, zone.ArcDirection == "up", cw, ch);
                continue;
            }

            // ── Straight text ─────────────────────────────────────────────────
            var hAlign = effectiveAlign switch
            {
                "left"  => HorizontalAlignment.Left,
                "right" => HorizontalAlignment.Right,
                _       => HorizontalAlignment.Center,
            };
            float originX = effectiveAlign switch
            {
                "left"  => zx,
                "right" => zx + zw,
                _       => zx + zw / 2f,
            };
            var origin = new PointF(originX, zy + zh / 2f);
            var opts = new RichTextOptions(font)
            {
                Origin              = origin,
                HorizontalAlignment = hAlign,
                VerticalAlignment   = VerticalAlignment.Center,
                WrappingLength      = zw,
            };

            canvas.Mutate(ctx =>
            {
                if (strokePx > 0)
                {
                    ctx.DrawText(opts, text, Brushes.Solid(fillColor), Pens.Solid(strokeColor, strokePx));
                }
                else
                {
                    var shadowOpts = new RichTextOptions(font)
                    {
                        Origin              = new PointF(origin.X + 2, origin.Y + 2),
                        HorizontalAlignment = hAlign,
                        VerticalAlignment   = VerticalAlignment.Center,
                        WrappingLength      = zw,
                    };
                    ctx.DrawText(shadowOpts, text, Color.FromRgba(0, 0, 0, 180));
                    ctx.DrawText(opts, text, fillColor);
                }
            });
        }
    }

    /// <summary>
    /// Renders text along a circular arc by placing each glyph at the correct
    /// arc position and rotation angle.
    /// </summary>
    private void RenderArcText(
        Image<Rgba32> canvas,
        string text,
        Font font,
        Color fillColor,
        Color strokeColor,
        float strokePx,
        int zx, int zy, int zw, int zh,
        double arcRx, double arcRy, bool arcUp,
        int cw, int ch)
    {
        double halfW = zw / 2.0;
        double Rx    = Math.Max(halfW + 1.0, arcRx * ch);   // horizontal semi-axis
        double Ry    = Math.Max(1.0,          arcRy * ch);   // vertical semi-axis
        double cx    = zx + zw / 2.0;
        double cy    = zy + zh / 2.0;
        // Ellipse centre is directly above/below zone centre by Ry
        double eccy  = arcUp ? cy + Ry : cy - Ry;

        // Measure per-character positions along the straight baseline
        var measOpts = new TextOptions(font) { Origin = PointF.Empty };
        if (!TextMeasurer.TryMeasureCharacterBounds(text, measOpts, out ReadOnlySpan<GlyphBounds> charBounds)
            || charBounds.IsEmpty)
        {
            // Fallback: straight text at zone centre
            canvas.Mutate(ctx => ctx.DrawText(
                new RichTextOptions(font)
                {
                    Origin = new PointF((float)cx, (float)cy),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center,
                }, text, fillColor));
            return;
        }

        float totalWidth = charBounds[^1].Bounds.Right;
        int   sz         = (int)(font.Size * 3f) + 8;

        foreach (var cb in charBounds)
        {
            if (cb.StringIndex < 0 || cb.StringIndex >= text.Length) continue;
            string charStr = text[cb.StringIndex].ToString();

            // Map baseline x-offset → ellipse point
            float  charCenter = cb.Bounds.Left + cb.Bounds.Width / 2f;
            double d          = Math.Clamp(charCenter - totalWidth / 2.0, -Rx, Rx);
            double sqrtTerm   = Math.Sqrt(Math.Max(0.0, 1.0 - (d / Rx) * (d / Rx)));

            double charX = cx + d;
            double charY = arcUp
                ? eccy - Ry * sqrtTerm   // upper arc: above ellipse centre
                : eccy + Ry * sqrtTerm;  // lower arc: below ellipse centre

            // Tangent rotation: d(x,y)/dt at this point on the ellipse
            // For 'up'  (upper arc, θ ∈ (-π,0)): rot = atan2( Ry*(d/Rx),  Rx*sqrtTerm)
            // For 'down'(lower arc, θ ∈ (0,π)) : rot = atan2(-Ry*(d/Rx),  Rx*sqrtTerm)
            double rotRad = arcUp
                ? Math.Atan2( Ry * (d / Rx), Rx * sqrtTerm)
                : Math.Atan2(-Ry * (d / Rx), Rx * sqrtTerm);
            float  rotDeg = (float)(rotRad * 180.0 / Math.PI);

            // Draw single glyph onto a transparent square, then rotate and composite
            using var tmp = new Image<Rgba32>(sz, sz, Color.Transparent);
            var charOpts = new RichTextOptions(font)
            {
                Origin              = new PointF(sz / 2f, sz / 2f),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
            };

            if (strokePx > 0)
            {
                tmp.Mutate(c2 => c2.DrawText(charOpts, charStr,
                    Brushes.Solid(fillColor), Pens.Solid(strokeColor, strokePx)));
            }
            else
            {
                var shadowOpts = new RichTextOptions(font)
                {
                    Origin              = new PointF(sz / 2f + 2, sz / 2f + 2),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center,
                };
                tmp.Mutate(c2 =>
                {
                    c2.DrawText(shadowOpts, charStr, Color.FromRgba(0, 0, 0, 180));
                    c2.DrawText(charOpts,   charStr, fillColor);
                });
            }

            tmp.Mutate(c2 => c2.Rotate(rotDeg));

            int pasteX = (int)Math.Round(charX - tmp.Width  / 2.0);
            int pasteY = (int)Math.Round(charY - tmp.Height / 2.0);
            canvas.Mutate(c2 => c2.DrawImage(tmp, new Point(pasteX, pasteY), 1f));
        }
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
    /// Appends SVG &lt;text&gt; elements for each defined text zone,
    /// merging zone defaults with optional per-zone user style overrides.
    /// </summary>
    private void AppendSvgTextZones(
        StringBuilder svg,
        string? textZonesJson,
        string? textConfigJson,
        string? textStyleOverridesJson,
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
        try { zones = JsonSerializer.Deserialize<TextZoneDto[]>(textZonesJson, _jsonOpts); }
        catch { return; }
        if (zones is null || zones.Length == 0) return;

        var overrides = ParseStyleOverrides(textStyleOverridesJson);

        foreach (var zone in zones)
        {
            textValues.TryGetValue(zone.Id, out var text);
            text = string.IsNullOrWhiteSpace(text) ? zone.DefaultText ?? "" : text;
            if (string.IsNullOrWhiteSpace(text)) continue;

            var ov = overrides.GetValueOrDefault(zone.Id);

            int zx = (int)Math.Round(zone.X * cw);
            int zy = (int)Math.Round(zone.Y * ch);
            int zw = Math.Max(1, (int)Math.Round(zone.W * cw));
            int zh = Math.Max(1, (int)Math.Round(zone.H * ch));

            double effectiveFontSize = ov?.FontSize    ?? zone.FontSize;
            string effectiveFontName = ov?.FontFamily  ?? zone.FontFamily;
            string effectiveColor    = ov?.Color       ?? zone.Color;
            double effectiveStrokeW  = ov?.StrokeWidth ?? zone.StrokeWidth;
            string effectiveStrokeC  = ov?.StrokeColor ?? zone.StrokeColor;
            string effectiveAlign    = ov?.Align       ?? zone.Align;

            float fontSize = Math.Max(8f, (float)(zh * effectiveFontSize / 100.0));
            string weight  = zone.Id == "title" ? "bold" : "normal";

            var (textAnchor, cx) = effectiveAlign switch
            {
                "left"  => ("start",  zx),
                "right" => ("end",    zx + zw),
                _       => ("middle", zx + zw / 2),
            };
            int cy = zy + zh / 2;

            string fontAttr = $"font-family=\"{XmlEscape(effectiveFontName)}, Arial, sans-serif\" " +
                              $"font-size=\"{fontSize:F0}\" font-weight=\"{weight}\" " +
                              $"text-anchor=\"{textAnchor}\" dominant-baseline=\"middle\"";

            var escaped = XmlEscape(text);

            if (zone.ArcEnabled)
            {
                // ── Arc text via SVG textPath ────────────────────────────────
                bool   arcUp    = zone.ArcDirection != "down";
                double halfW_px = zw / 2.0;
                double zoneCx   = zx + zw / 2.0;
                double Rx_px    = Math.Max(halfW_px + 1.0, zone.ArcRx * ch);
                double Ry_px    = Math.Max(1.0,             zone.ArcRy * ch);

                double ratio    = Math.Min(1.0, halfW_px / Rx_px);
                double yOff     = Ry_px * (1.0 - Math.Sqrt(1.0 - ratio * ratio));
                double arcSy    = arcUp ? cy + yOff : cy - yOff;
                double arcSx    = zoneCx - halfW_px;
                double arcEx    = zoneCx + halfW_px;
                int    sweep    = arcUp ? 0 : 1;

                string arcD   = $"M {arcSx:F1},{arcSy:F1} A {Rx_px:F1},{Ry_px:F1} 0 0 {sweep} {arcEx:F1},{arcSy:F1}";
                string pathId = $"arc_{XmlEscape(zone.Id)}";

                string fontAttrsArc = $"font-family=\"{XmlEscape(effectiveFontName)}, Arial, sans-serif\" " +
                                      $"font-size=\"{fontSize:F0}\" font-weight=\"{weight}\" text-anchor=\"middle\"";

                svg.AppendLine($"""  <defs><path id="{pathId}" d="{arcD}"/></defs>""");

                if (effectiveStrokeW > 0)
                {
                    float strokePx = Math.Max(0.5f, (float)(fontSize * effectiveStrokeW / 100.0));
                    svg.AppendLine($"""  <text {fontAttrsArc} fill="{effectiveColor}" stroke="{effectiveStrokeC}" stroke-width="{strokePx:F1}" paint-order="stroke">""");
                    svg.AppendLine($"""    <textPath href="#{pathId}" startOffset="50%">{escaped}</textPath>""");
                    svg.AppendLine($"""  </text>""");
                }
                else
                {
                    // Shadow path offset by (2,2)
                    string shadowId = $"arc_sh_{XmlEscape(zone.Id)}";
                    string shadowD  = $"M {arcSx + 2:F1},{arcSy + 2:F1} A {Rx_px:F1},{Ry_px:F1} 0 0 {sweep} {arcEx + 2:F1},{arcSy + 2:F1}";
                    svg.AppendLine($"""  <defs><path id="{shadowId}" d="{shadowD}"/></defs>""");
                    svg.AppendLine($"""  <text {fontAttrsArc} fill="#000000" fill-opacity="0.7">""");
                    svg.AppendLine($"""    <textPath href="#{shadowId}" startOffset="50%">{escaped}</textPath>""");
                    svg.AppendLine($"""  </text>""");
                    svg.AppendLine($"""  <text {fontAttrsArc} fill="{effectiveColor}">""");
                    svg.AppendLine($"""    <textPath href="#{pathId}" startOffset="50%">{escaped}</textPath>""");
                    svg.AppendLine($"""  </text>""");
                }
            }
            else
            {
                // ── Straight text ────────────────────────────────────────────
                if (effectiveStrokeW > 0)
                {
                    float strokePx = Math.Max(0.5f, (float)(fontSize * effectiveStrokeW / 100.0));
                    svg.AppendLine($"""  <text x="{cx}" y="{cy}" {fontAttr} fill="{effectiveColor}" stroke="{effectiveStrokeC}" stroke-width="{strokePx:F1}">{escaped}</text>""");
                }
                else
                {
                    // Drop shadow + fill
                    svg.AppendLine($"""  <text x="{cx + 2}" y="{cy + 2}" {fontAttr} fill="#000000" fill-opacity="0.7">{escaped}</text>""");
                    svg.AppendLine($"""  <text x="{cx}" y="{cy}" {fontAttr} fill="{effectiveColor}">{escaped}</text>""");
                }
            }
        }
    }

    private static string XmlEscape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    /// <summary>Parses a CSS hex color string ('#rrggbb' or '#rrggbbaa'). Falls back to <paramref name="fallback"/>.</summary>
    private static Color ParseColor(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        if (Color.TryParse(hex, out var c)) return c;
        return fallback;
    }

    /// <summary>
    /// Parses TextStyleOverridesJson into a lookup dictionary keyed by zone id.
    /// Returns an empty dictionary when json is null or malformed.
    /// </summary>
    private Dictionary<string, TextStyleOverrideDto> ParseStyleOverrides(string? json)
    {
        var result = new Dictionary<string, TextStyleOverrideDto>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, TextStyleOverrideDto>>(json, _jsonOpts);
            if (parsed is not null)
                foreach (var kv in parsed) result[kv.Key] = kv.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse TextStyleOverridesJson — using zone defaults.");
        }
        return result;
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

    // ── Shape masking ─────────────────────────────────────────────────────────

    /// <summary>
    /// Applies a shape mask to the composited subject image by zeroing the alpha channel
    /// of pixels that fall outside the slot's defined shape (ellipse or polygon).
    /// For "rect" this is a no-op — the crop boundary already clips correctly.
    /// </summary>
    /// <param name="dstX">Canvas-pixel X where the image will be drawn (= image origin).</param>
    /// <param name="dstY">Canvas-pixel Y where the image will be drawn (= image origin).</param>
    private static void ApplyShapeMask(
        Image<Rgba32> img, SubjectSlot slot,
        int cw, int ch, int dstX, int dstY)
    {
        if (slot.Shape == "ellipse")
        {
            // Ellipse inscribed within the SLOT bounding box (not the image bounding box).
            // Using slot dimensions ensures a consistent shape regardless of letterboxing,
            // matching the CSS `ellipse(50% 50% at 50% 50%)` clip applied to the slot div.
            var (_, _, slotW, slotH) = LayoutCalculator.ToPixels(slot.Rect, cw, ch);
            float cx = img.Width  / 2f;
            float cy = img.Height / 2f;
            float rx = slotW / 2f;
            float ry = slotH / 2f;
            img.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < img.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < img.Width; x++)
                    {
                        float dx = (x - cx) / rx;
                        float dy = (y - cy) / ry;
                        if (dx * dx + dy * dy > 1f)
                            row[x] = new Rgba32(0, 0, 0, 0);
                    }
                }
            });
        }
        else if (slot.Shape == "polygon" && slot.Points is { Length: >= 3 })
        {
            // Convert canvas-normalized polygon points to image-local pixel coordinates.
            // The image occupies canvas pixels starting at (dstX, dstY), so:
            //   imageX = p[0] * cw - dstX
            //   imageY = p[1] * ch - dstY
            var polyPts = slot.Points
                .Select(p => new PointF((float)(p[0] * cw - dstX), (float)(p[1] * ch - dstY)))
                .ToArray();

            // Rasterise a white filled polygon onto a transparent mask
            using var mask = new Image<Rgba32>(img.Width, img.Height, Color.Transparent);
            mask.Mutate(ctx => ctx.FillPolygon(Color.White, polyPts));

            // Zero alpha where the mask is transparent
            img.ProcessPixelRows(mask, (imgAcc, maskAcc) =>
            {
                for (int y = 0; y < img.Height; y++)
                {
                    var imgRow  = imgAcc.GetRowSpan(y);
                    var maskRow = maskAcc.GetRowSpan(y);
                    for (int x = 0; x < img.Width; x++)
                    {
                        if (maskRow[x].A == 0)
                            imgRow[x] = new Rgba32(0, 0, 0, 0);
                    }
                }
            });
        }
        // "rect": no masking — the bounding-box crop already defines the boundary
    }

    /// <summary>
    /// Builds an SVG &lt;clipPath&gt; element string for the given slot shape, or null for "rect".
    /// Coordinates are in canvas pixels (SVG user-coordinate space).
    /// </summary>
    private static string? BuildSvgClipPath(
        string id, SubjectSlot slot,
        int cw, int ch, int dstX, int dstY, int drawW, int drawH)
    {
        if (slot.Shape == "ellipse")
        {
            // Use slot pixel dimensions for rx/ry (not image dimensions) so the ellipse
            // inscribed in the SVG matches the CSS `ellipse(50% 50% at 50% 50%)` applied
            // to the slot div — consistent with ApplyShapeMask.
            var (_, _, slotW, slotH) = LayoutCalculator.ToPixels(slot.Rect, cw, ch);
            double cx = dstX + drawW / 2.0;
            double cy = dstY + drawH / 2.0;
            double rx = slotW / 2.0;
            double ry = slotH / 2.0;
            return $"""<clipPath id="{id}"><ellipse cx="{cx:F1}" cy="{cy:F1}" rx="{rx:F1}" ry="{ry:F1}"/></clipPath>""";
        }
        if (slot.Shape == "polygon" && slot.Points is { Length: >= 3 })
        {
            var pts = string.Join(" ", slot.Points.Select(p => $"{p[0] * cw:F1},{p[1] * ch:F1}"));
            return $"""<clipPath id="{id}"><polygon points="{pts}"/></clipPath>""";
        }
        return null;
    }

    // ── DTOs used only within this file ──────────────────────────────────────

    private sealed record TextZoneDto(
        string Id,
        double X,
        double Y,
        double W,
        double H,
        string? DefaultText    = null,
        double  FontSize       = 50,      // % of zone height
        string  FontFamily     = "Arial",
        string  Color          = "#ffffff",
        double  StrokeWidth    = 0,       // % of zone height
        string  StrokeColor    = "#000000",
        string  Align          = "center",
        bool    ArcEnabled     = false,
        double  ArcRx          = 0.7,     // horizontal semi-axis, fraction of canvas height
        double  ArcRy          = 0.5,     // vertical semi-axis, fraction of canvas height
        string  ArcDirection   = "up");   // "up" or "down"

    /// <summary>Per-zone typography overrides supplied by the user in Step 6.</summary>
    private sealed record TextStyleOverrideDto(
        double?  FontSize    = null,
        string?  FontFamily  = null,
        string?  Color       = null,
        double?  StrokeWidth = null,
        string?  StrokeColor = null,
        string?  Align       = null);

    /// <summary>
    /// Background crop transform parsed from BgCropJson.
    /// Scale=1 = contain-fit (full image visible); offset fractions of canvas size (0 = centred).
    /// </summary>
    private sealed record BgCropEntry(double Scale, double OffsetX, double OffsetY)
    {
        public static BgCropEntry Default => new(1.0, 0.0, 0.0);

        public static BgCropEntry Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Default;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                return new BgCropEntry(
                    root.TryGetProperty("scale",   out var s) ? s.GetDouble() : 1.0,
                    root.TryGetProperty("offsetX", out var x) ? x.GetDouble() : 0.0,
                    root.TryGetProperty("offsetY", out var y) ? y.GetDouble() : 0.0);
            }
            catch { return Default; }
        }
    }
}
