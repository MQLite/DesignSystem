using System.Text.Json;
using System.Text.Json.Serialization;
using DesignSystem.Infrastructure.Rendering.Models;

namespace DesignSystem.Infrastructure.Rendering.Helpers;

/// <summary>
/// Parses BackgroundLayout.SubjectCropFramesJson into strongly-typed <see cref="SubjectCropFrame"/> models.
/// Follows the same conventions as <see cref="SlotParser"/> — handles missing fields with safe defaults.
/// </summary>
public static class CropFrameParser
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static IReadOnlyList<SubjectCropFrame> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var raw = JsonSerializer.Deserialize<List<CropFrameRaw>>(json, _opts);
            if (raw is null || raw.Count == 0)
                return [];

            return raw.Select((r, i) => new SubjectCropFrame
            {
                Id           = string.IsNullOrWhiteSpace(r.Id) ? $"crop_{i}" : r.Id,
                Rect         = new RectN(r.X, r.Y, r.W, r.H),
                Shape        = r.Shape ?? "rect",
                AspectRatio  = r.AspectRatio,
                AllowUserMove  = r.AllowUserMove ?? true,
                AllowUserScale = r.AllowUserScale ?? true,
            }).ToList();
        }
        catch (JsonException)
        {
            // Malformed JSON — return empty rather than crash the compose pipeline
            return [];
        }
    }

    private sealed class CropFrameRaw
    {
        public string? Id { get; set; }

        // Normalised canvas coordinates (required)
        public double X { get; set; }
        public double Y { get; set; }
        public double W { get; set; }
        public double H { get; set; }

        public string? Shape { get; set; }
        public double? AspectRatio { get; set; }

        [JsonPropertyName("allowUserMove")]
        public bool? AllowUserMove { get; set; }

        [JsonPropertyName("allowUserScale")]
        public bool? AllowUserScale { get; set; }
    }
}
