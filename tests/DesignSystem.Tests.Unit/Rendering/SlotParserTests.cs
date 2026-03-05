using Xunit;
using DesignSystem.Infrastructure.Rendering.Helpers;
using FluentAssertions;

namespace DesignSystem.Tests.Unit.Rendering;

public sealed class SlotParserTests
{
    // ── Null / empty input ───────────────────────────────────────────────────

    [Fact]
    public void Parse_Null_ReturnsEmpty()
        => SlotParser.Parse(null).Should().BeEmpty();

    [Fact]
    public void Parse_EmptyString_ReturnsEmpty()
        => SlotParser.Parse("").Should().BeEmpty();

    [Fact]
    public void Parse_WhitespaceString_ReturnsEmpty()
        => SlotParser.Parse("   ").Should().BeEmpty();

    [Fact]
    public void Parse_EmptyArray_ReturnsEmpty()
        => SlotParser.Parse("[]").Should().BeEmpty();

    // ── Error tolerance ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_InvalidJson_DoesNotThrow_ReturnsEmpty()
        => SlotParser.Parse("not valid json ][").Should().BeEmpty();

    [Fact]
    public void Parse_MalformedObject_DoesNotThrow_ReturnsEmpty()
        => SlotParser.Parse("{x:1}").Should().BeEmpty();

    // ── Seed data format (minimal: only x, y, w, h) ──────────────────────────

    [Fact]
    public void Parse_MinimalSlot_CoordinatesCorrect()
    {
        var slots = SlotParser.Parse("""[{"x":0.25,"y":0.15,"w":0.50,"h":0.60}]""");

        slots.Should().HaveCount(1);
        var s = slots[0];
        s.Rect.X.Should().Be(0.25);
        s.Rect.Y.Should().Be(0.15);
        s.Rect.W.Should().Be(0.50);
        s.Rect.H.Should().Be(0.60);
    }

    [Fact]
    public void Parse_MinimalSlot_DefaultsApplied()
    {
        var s = SlotParser.Parse("""[{"x":0,"y":0,"w":1,"h":1}]""")[0];

        s.Id.Should().Be("slot_0");
        s.Anchor.Should().Be("BottomCenter");
        s.FitMode.Should().Be("Contain");
        s.AllowUserMove.Should().BeTrue();
        s.AllowUserScale.Should().BeTrue();
        s.MinScale.Should().Be(0.5);
        s.MaxScale.Should().Be(2.0);
    }

    // ── Full slot with all fields ────────────────────────────────────────────

    [Fact]
    public void Parse_FullSlot_AllFieldsPreserved()
    {
        const string json = """
            [{
              "id": "hero",
              "x": 0.1, "y": 0.2, "w": 0.8, "h": 0.6,
              "anchor": "Center",
              "fitMode": "Cover",
              "allowUserMove": false,
              "allowUserScale": false,
              "minScale": 0.3,
              "maxScale": 1.5
            }]
            """;
        var s = SlotParser.Parse(json).Single();

        s.Id.Should().Be("hero");
        s.Rect.X.Should().Be(0.1);
        s.Anchor.Should().Be("Center");
        s.FitMode.Should().Be("Cover");
        s.AllowUserMove.Should().BeFalse();
        s.AllowUserScale.Should().BeFalse();
        s.MinScale.Should().Be(0.3);
        s.MaxScale.Should().Be(1.5);
    }

    // ── Multiple slots ───────────────────────────────────────────────────────

    [Fact]
    public void Parse_MultipleSlots_CountCorrect()
    {
        const string json = """
            [
              {"x":0.0,"y":0.0,"w":0.5,"h":1.0},
              {"x":0.5,"y":0.0,"w":0.5,"h":1.0}
            ]
            """;
        var slots = SlotParser.Parse(json);

        slots.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_MultipleSlots_IdsGeneratedByIndex()
    {
        const string json = """
            [
              {"x":0,"y":0,"w":0.5,"h":1},
              {"id":"named","x":0.5,"y":0,"w":0.5,"h":1}
            ]
            """;
        var slots = SlotParser.Parse(json);

        slots[0].Id.Should().Be("slot_0");   // no id in JSON → generated
        slots[1].Id.Should().Be("named");     // explicit id preserved
    }
}
