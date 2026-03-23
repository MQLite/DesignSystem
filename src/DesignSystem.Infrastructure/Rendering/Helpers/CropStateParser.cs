using System.Text.Json;
using System.Text.Json.Serialization;
using DesignSystem.Infrastructure.Rendering.Models;

namespace DesignSystem.Infrastructure.Rendering.Helpers;

/// <summary>
/// Parses SubjectCropStateJson (from ComposeRequest or DesignProject) into
/// strongly-typed <see cref="CropStateEntry"/> records.
/// </summary>
public static class CropStateParser
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static IReadOnlyList<CropStateEntry> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var raw = JsonSerializer.Deserialize<List<CropStateRaw>>(json, _opts);
            if (raw is null || raw.Count == 0)
                return [];

            return raw
                .Where(r => !string.IsNullOrWhiteSpace(r.SlotId))
                .Select(r => new CropStateEntry
                {
                    SlotId  = r.SlotId!,
                    OffsetX = r.OffsetX,
                    OffsetY = r.OffsetY,
                    Scale   = r.Scale ?? 1.0,
                })
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Look up the state for a specific slot id.
    /// Returns a default-centred, unscaled entry when not found.
    /// </summary>
    public static CropStateEntry GetOrDefault(IReadOnlyList<CropStateEntry> entries, string slotId)
        => entries.FirstOrDefault(e => e.SlotId == slotId)
           ?? new CropStateEntry { SlotId = slotId, OffsetX = 0, OffsetY = 0, Scale = 1.0 };

    private sealed class CropStateRaw
    {
        public string? SlotId { get; set; }

        [JsonPropertyName("offsetX")]
        public double OffsetX { get; set; }

        [JsonPropertyName("offsetY")]
        public double OffsetY { get; set; }

        public double? Scale { get; set; }
    }
}
