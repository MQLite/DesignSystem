using Xunit;
using DesignSystem.Infrastructure.Rendering.Helpers;
using DesignSystem.Infrastructure.Rendering.Models;
using FluentAssertions;

namespace DesignSystem.Tests.Unit.Rendering;

public sealed class LayoutCalculatorTests
{
    // ── ToPixels ─────────────────────────────────────────────────────────────

    [Fact]
    public void ToPixels_FullCanvas_ReturnsCanvasSize()
    {
        var (x, y, w, h) = LayoutCalculator.ToPixels(new RectN(0, 0, 1, 1), 1000, 2000);
        (x, y, w, h).Should().Be((0, 0, 1000, 2000));
    }

    [Fact]
    public void ToPixels_HalfQuadrant_CorrectPixels()
    {
        // rect(0.5, 0.25, 0.5, 0.5) on 1000x2000
        var (x, y, w, h) = LayoutCalculator.ToPixels(new RectN(0.5, 0.25, 0.5, 0.5), 1000, 2000);
        (x, y, w, h).Should().Be((500, 500, 500, 1000));
    }

    // ── CalculatePlacement — FitMode: Contain ─────────────────────────────────

    [Theory]
    [InlineData(200, 400, 250, 0,  500, 1000)] // portrait subject → height-constrained
    [InlineData(400, 200, 0,  500, 1000, 500)] // landscape subject → width-constrained
    [InlineData(500, 500, 0,  0,  1000, 1000)] // square subject → fills slot
    public void CalculatePlacement_Contain_BottomCenter_FullSlot(
        int srcW, int srcH,
        int expectedX, int expectedY, int expectedW, int expectedH)
    {
        // Slot covers the entire 1000×1000 canvas
        var slot = MakeSlot(0, 0, 1, 1, anchor: "BottomCenter", fitMode: "Contain");
        var p = LayoutCalculator.CalculatePlacement(srcW, srcH, slot, 1000, 1000);

        p.Should().Be(new PlacementResult(expectedX, expectedY, expectedW, expectedH));
    }

    [Fact]
    public void CalculatePlacement_Contain_BottomCenter_OffsetSlot()
    {
        // Slot rect(0.1, 0.1, 0.8, 0.8) on 1000×1000 → slotX=100, slotY=100, slotW=800, slotH=800
        // Subject 200×400 (portrait): scale = min(800/200, 800/400) = min(4, 2) = 2
        // drawW=400, drawH=800
        // BottomCenter: x = 100 + (800-400)/2 = 300; y = 100 + 800-800 = 100
        var slot = MakeSlot(0.1, 0.1, 0.8, 0.8);
        var p = LayoutCalculator.CalculatePlacement(200, 400, slot, 1000, 1000);

        p.Should().Be(new PlacementResult(300, 100, 400, 800));
    }

    // ── FitMode: Cover ────────────────────────────────────────────────────────

    [Fact]
    public void CalculatePlacement_Cover_BottomCenter_LandscapeSubject()
    {
        // Subject 400×200, slot full 1000×1000
        // Cover: scale = max(1000/400, 1000/200) = max(2.5, 5) = 5
        // drawW=2000, drawH=1000
        // BottomCenter: x = (1000-2000)/2 = -500; y = 0+1000-1000 = 0
        var slot = MakeSlot(0, 0, 1, 1, fitMode: "Cover");
        var p = LayoutCalculator.CalculatePlacement(400, 200, slot, 1000, 1000);

        p.Should().Be(new PlacementResult(-500, 0, 2000, 1000));
    }

    // ── FitMode: Stretch ─────────────────────────────────────────────────────

    [Fact]
    public void CalculatePlacement_Stretch_AlwaysFillsSlot()
    {
        // Subject dimensions are irrelevant for Stretch
        var slot = MakeSlot(0, 0, 1, 1, fitMode: "Stretch");
        var p = LayoutCalculator.CalculatePlacement(123, 456, slot, 1000, 1000);

        p.Should().Be(new PlacementResult(0, 0, 1000, 1000));
    }

    // ── Anchor variants ───────────────────────────────────────────────────────

    [Fact]
    public void CalculatePlacement_Contain_Center_CentresVertically()
    {
        // Landscape subject 400×200 → Contain in 1000×1000: drawW=1000, drawH=500
        // Center: x=(1000-1000)/2=0; y=(1000-500)/2=250
        var slot = MakeSlot(0, 0, 1, 1, anchor: "Center");
        var p = LayoutCalculator.CalculatePlacement(400, 200, slot, 1000, 1000);

        p.Should().Be(new PlacementResult(0, 250, 1000, 500));
    }

    [Fact]
    public void CalculatePlacement_Contain_TopCenter_AlignsToTop()
    {
        // Landscape subject 400×200 → drawW=1000, drawH=500
        // TopCenter: x=0; y=slotY=0
        var slot = MakeSlot(0, 0, 1, 1, anchor: "TopCenter");
        var p = LayoutCalculator.CalculatePlacement(400, 200, slot, 1000, 1000);

        p.Should().Be(new PlacementResult(0, 0, 1000, 500));
    }

    [Fact]
    public void CalculatePlacement_UnknownAnchor_FallsBackToBottomCenter()
    {
        var slot = MakeSlot(0, 0, 1, 1, anchor: "SomeUnknownAnchor");
        var pUnknown = LayoutCalculator.CalculatePlacement(400, 200, slot, 1000, 1000);
        var pBottom  = LayoutCalculator.CalculatePlacement(400, 200, MakeSlot(0, 0, 1, 1, anchor: "BottomCenter"), 1000, 1000);

        pUnknown.Should().Be(pBottom);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static SubjectSlot MakeSlot(
        double x, double y, double w, double h,
        string anchor = "BottomCenter",
        string fitMode = "Contain") => new()
    {
        Id = "test",
        Rect = new RectN(x, y, w, h),
        Anchor = anchor,
        FitMode = fitMode,
    };
}
