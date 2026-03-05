using Xunit;
using DesignSystem.Infrastructure.Rendering;
using DesignSystem.Infrastructure.Rendering.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DesignSystem.Tests.Unit.Rendering;

/// <summary>
/// Tests for ComposerEngine skeleton behaviour.
/// Uses a temp directory so no real storage folder is needed.
/// </summary>
public sealed class ComposerEngineTests : IDisposable
{
    private readonly ComposerEngine _engine =
        new(NullLogger<ComposerEngine>.Instance);

    // Isolated temp dir per test class instance
    private readonly string _storageRoot =
        Path.Combine(Path.GetTempPath(), $"DesignSystem_Tests_{Guid.NewGuid():N}");

    public ComposerEngineTests() => Directory.CreateDirectory(_storageRoot);

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
    }

    // ── Guard: invalid inputs ────────────────────────────────────────────────

    [Fact]
    public async Task ComposePreviewAsync_EmptyBackgroundPath_Throws()
    {
        var req = Valid() with { BackgroundSourcePath = "" };
        await Assert.ThrowsAsync<ArgumentException>(() => _engine.ComposePreviewAsync(req));
    }

    [Fact]
    public async Task ComposePreviewAsync_ZeroCanvasWidth_Throws()
    {
        var req = Valid() with { CanvasWidthPx = 0 };
        await Assert.ThrowsAsync<ArgumentException>(() => _engine.ComposePreviewAsync(req));
    }

    [Fact]
    public async Task ComposePreviewAsync_NegativeCanvasHeight_Throws()
    {
        var req = Valid() with { CanvasHeightPx = -1 };
        await Assert.ThrowsAsync<ArgumentException>(() => _engine.ComposePreviewAsync(req));
    }

    [Fact]
    public async Task ComposePreviewAsync_EmptyStorageRoot_Throws()
    {
        var req = Valid() with { StorageRootPath = "" };
        await Assert.ThrowsAsync<ArgumentException>(() => _engine.ComposePreviewAsync(req));
    }

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ComposePreviewAsync_ValidRequest_ReturnsDimensionsMatchingCanvas()
    {
        var result = await _engine.ComposePreviewAsync(Valid());

        result.WidthPx.Should().Be(1754);
        result.HeightPx.Should().Be(2480);
    }

    [Fact]
    public async Task ComposePreviewAsync_ValidRequest_OutputTypeIsPreviewPng()
    {
        var result = await _engine.ComposePreviewAsync(Valid());
        result.OutputType.Should().Be("preview-png");
    }

    [Fact]
    public async Task ComposePreviewAsync_ValidRequest_OutputPathFollowsConvention()
    {
        var result = await _engine.ComposePreviewAsync(Valid());

        result.OutputRelativePath.Should().StartWith("storage/previews/");
        result.OutputRelativePath.Should().EndWith("_preview.png");
    }

    [Fact]
    public async Task ComposePreviewAsync_ValidRequest_PlaceholderFileIsWritten()
    {
        var result = await _engine.ComposePreviewAsync(Valid());

        var fileName = Path.GetFileName(result.OutputRelativePath);
        var absPath = Path.Combine(_storageRoot, "previews", fileName);
        File.Exists(absPath).Should().BeTrue("skeleton must write a placeholder file");
    }

    [Fact]
    public async Task ComposePreviewAsync_CalledTwice_EachCallProducesDistinctFile()
    {
        var r1 = await _engine.ComposePreviewAsync(Valid());
        var r2 = await _engine.ComposePreviewAsync(Valid());

        r1.OutputRelativePath.Should().NotBe(r2.OutputRelativePath);
    }

    [Fact]
    public async Task ComposePreviewAsync_NoSubject_StillSucceeds()
    {
        var req = Valid() with { SubjectCutoutPath = null };
        var result = await _engine.ComposePreviewAsync(req);
        result.OutputRelativePath.Should().NotBeNullOrEmpty();
    }

    // ── Builder ───────────────────────────────────────────────────────────────

    private ComposeRequest Valid() => new()
    {
        BackgroundSourcePath = "storage/backgrounds/seeded/lily_source.png",
        SubjectCutoutPath    = null,
        SubjectSlotsJson     = """[{"x":0.25,"y":0.15,"w":0.50,"h":0.60}]""",
        TextConfigJson       = "{}",
        TargetDpi            = 150,
        CanvasWidthPx        = 1754,
        CanvasHeightPx       = 2480,
        StorageRootPath      = _storageRoot,
    };
}
