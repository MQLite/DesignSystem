using DesignSystem.Infrastructure.Rendering.Models;

namespace DesignSystem.Infrastructure.Rendering.Helpers;

/// <summary>
/// Pure layout math — no drawing, no I/O.
/// All methods are static and unit-testable without any infrastructure.
/// </summary>
public static class LayoutCalculator
{
    /// <summary>
    /// Converts a normalized <see cref="RectN"/> to absolute pixel coordinates on the canvas.
    /// </summary>
    public static (int X, int Y, int W, int H) ToPixels(RectN rect, int canvasW, int canvasH) => (
        (int)Math.Round(rect.X * canvasW),
        (int)Math.Round(rect.Y * canvasH),
        (int)Math.Round(rect.W * canvasW),
        (int)Math.Round(rect.H * canvasH)
    );

    /// <summary>
    /// Calculates the draw position and size for a subject image placed inside a slot,
    /// using the slot's <see cref="SubjectSlot.FitMode"/> and <see cref="SubjectSlot.Anchor"/>.
    /// <para>No actual drawing is performed — returns computed pixel bounds only.</para>
    /// </summary>
    /// <param name="subjectW">Source image width in pixels.</param>
    /// <param name="subjectH">Source image height in pixels.</param>
    /// <param name="slot">Slot definition from the background layout.</param>
    /// <param name="canvasW">Canvas width in pixels.</param>
    /// <param name="canvasH">Canvas height in pixels.</param>
    public static PlacementResult CalculatePlacement(
        int subjectW,
        int subjectH,
        SubjectSlot slot,
        int canvasW,
        int canvasH)
    {
        var (slotX, slotY, slotW, slotH) = ToPixels(slot.Rect, canvasW, canvasH);

        var (drawW, drawH) = slot.FitMode switch
        {
            "Cover" => ScaleCover(subjectW, subjectH, slotW, slotH),
            "Stretch" => (slotW, slotH),
            _ => ScaleContain(subjectW, subjectH, slotW, slotH), // "Contain" is the default
        };

        // Horizontal: always center within slot
        int drawX = slotX + (slotW - drawW) / 2;

        // Vertical: determined by Anchor
        int drawY = slot.Anchor switch
        {
            "Center" => slotY + (slotH - drawH) / 2,
            "TopCenter" => slotY,
            _ => slotY + slotH - drawH, // "BottomCenter" default
        };

        return new PlacementResult(drawX, drawY, drawW, drawH);
    }

    // ── Private scaling helpers ──────────────────────────────────────────────

    private static (int W, int H) ScaleContain(int srcW, int srcH, int boxW, int boxH)
    {
        double scale = Math.Min((double)boxW / srcW, (double)boxH / srcH);
        return ((int)Math.Round(srcW * scale), (int)Math.Round(srcH * scale));
    }

    private static (int W, int H) ScaleCover(int srcW, int srcH, int boxW, int boxH)
    {
        double scale = Math.Max((double)boxW / srcW, (double)boxH / srcH);
        return ((int)Math.Round(srcW * scale), (int)Math.Round(srcH * scale));
    }
}
