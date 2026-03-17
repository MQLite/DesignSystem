namespace DesignSystem.Infrastructure.Rendering.Models;

/// <summary>
/// One entry in DesignProject.SubjectCropStateJson / ComposeRequest.SubjectCropStateJson.
/// Records how the user has panned and zoomed the subject photo within a specific crop frame.
/// offsetX / offsetY are fractions of the crop viewport size (0 = centred).
/// scale is a multiplier over the natural "cover" fit inside the crop viewport.
/// </summary>
public record CropStateEntry
{
    /// <summary>Matches SubjectCropFrame.Id in SubjectCropFramesJson.</summary>
    public string CropFrameId { get; init; } = string.Empty;

    /// <summary>Horizontal pan as a fraction of the crop viewport width. 0 = centred.</summary>
    public double OffsetX { get; init; }

    /// <summary>Vertical pan as a fraction of the crop viewport height. 0 = centred.</summary>
    public double OffsetY { get; init; }

    /// <summary>Zoom multiplier over the "cover" baseline. 1.0 = no extra zoom.</summary>
    public double Scale { get; init; } = 1.0;
}
