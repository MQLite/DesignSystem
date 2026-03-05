namespace DesignSystem.Infrastructure.Rendering.Models;

/// <summary>
/// Computed pixel placement of an image inside a slot.
/// Origin is top-left of the canvas.
/// </summary>
public record struct PlacementResult(int X, int Y, int W, int H);
