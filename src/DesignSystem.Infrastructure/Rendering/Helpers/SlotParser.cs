using System.Text.Json;
using System.Text.Json.Serialization;
using DesignSystem.Infrastructure.Rendering.Models;

namespace DesignSystem.Infrastructure.Rendering.Helpers;

/// <summary>
/// Parses BackgroundLayout.SubjectSlotsJson into strongly-typed <see cref="SubjectSlot"/> models.
/// Handles missing fields gracefully with safe defaults.
/// </summary>
public static class SlotParser
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static IReadOnlyList<SubjectSlot> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var raw = JsonSerializer.Deserialize<List<SlotRaw>>(json, _opts);
            if (raw is null || raw.Count == 0)
                return [];

            return raw.Select((r, i) => new SubjectSlot
            {
                // Seed data slots may omit Id — generate one from index
                Id = string.IsNullOrWhiteSpace(r.Id) ? $"slot_{i}" : r.Id,
                Rect = new RectN(r.X, r.Y, r.W, r.H),
                Anchor = r.Anchor ?? "BottomCenter",
                FitMode = r.FitMode ?? "Contain",
                AllowUserMove = r.AllowUserMove ?? true,
                AllowUserScale = r.AllowUserScale ?? true,
                MinScale = r.MinScale ?? 0.5,
                MaxScale = r.MaxScale ?? 2.0,
            }).ToList();
        }
        catch (JsonException)
        {
            // Malformed JSON — return empty rather than crash the compose pipeline
            return [];
        }
    }

    // Internal raw deserialization model — matches both seed format and future extended format
    private sealed class SlotRaw
    {
        public string? Id { get; set; }

        // Normalized coordinates (required)
        public double X { get; set; }
        public double Y { get; set; }
        public double W { get; set; }
        public double H { get; set; }

        // Optional overrides
        public string? Anchor { get; set; }
        public string? FitMode { get; set; }

        [JsonPropertyName("allowUserMove")]
        public bool? AllowUserMove { get; set; }

        [JsonPropertyName("allowUserScale")]
        public bool? AllowUserScale { get; set; }

        public double? MinScale { get; set; }
        public double? MaxScale { get; set; }
    }
}
