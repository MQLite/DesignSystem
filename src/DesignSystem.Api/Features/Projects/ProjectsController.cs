using System.Text.Json;
using System.Text.Json.Serialization;
using DesignSystem.Domain.Entities;
using DesignSystem.Domain.Enums;
using DesignSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DesignSystem.Api.Features.Projects;

// ── Shared sub-DTOs ──────────────────────────────────────────────────────────

public record LayerTransformDto(double X, double Y, double Scale, double Rotation);

public record CanvasLayoutDto(
    LayerTransformDto Background,
    LayerTransformDto Subject,
    LayerTransformDto Title,
    LayerTransformDto Subtitle,
    LayerTransformDto Footer);

public record TextConfigDto(string Title, string Subtitle, string Footer);

public record CropStateDto(
    string CropFrameId,
    double OffsetX,
    double OffsetY,
    double Scale);

// ── Request types ─────────────────────────────────────────────────────────────

/// <summary>Creates a new DesignProject linked to an existing BackgroundLayout.</summary>
public record CreateProjectRequest(
    Guid BackgroundLayoutId,
    Guid? SubjectAssetId,
    ProductType ProductType,
    OccasionType OccasionType);

/// <summary>
/// Replaces the project's text content.
/// All fields are optional strings (empty string = cleared).
/// Max lengths: Title 40, Subtitle 60, Footer 120.
/// </summary>
public record UpdateTextRequest(
    string Title,
    string Subtitle,
    string Footer);

/// <summary>
/// Replaces the project's subject crop state.
/// Each entry identifies a crop frame by id and records the user's pan/zoom.
/// Scale must be in [0.1, 10.0]. OffsetX/Y must be in [-1.0, 1.0].
/// </summary>
public record UpdateCropRequest(IReadOnlyList<CropStateDto> CropStates);

/// <summary>
/// Replaces the project's canvas layout (per-layer placement transforms).
/// All five layer keys are required. Scale must be in [0.01, 20]. Rotation in [-360, 360].
/// </summary>
public record UpdateAdjustmentsRequest(CanvasLayoutDto Layout);

// ── Response type ─────────────────────────────────────────────────────────────

public record ProjectDetail(
    Guid Id,
    Guid BackgroundLayoutId,
    Guid? SubjectAssetId,
    string ProductType,
    string OccasionType,
    TextConfigDto TextConfig,
    CanvasLayoutDto? CanvasLayout,
    IReadOnlyList<CropStateDto>? SubjectCropStates,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

// ── Controller ────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController : ControllerBase
{
    private readonly AppDbContext _db;
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public ProjectsController(AppDbContext db) => _db = db;

    // ── POST /api/projects ────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new DesignProject linked to an existing BackgroundLayout.
    /// Returns 404 if the layout or (optional) subject asset does not exist.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ProjectDetail>> Create(
        [FromBody] CreateProjectRequest request,
        CancellationToken ct)
    {
        var layoutExists = await _db.BackgroundLayouts
            .AnyAsync(l => l.Id == request.BackgroundLayoutId, ct);
        if (!layoutExists)
            return NotFound($"BackgroundLayout {request.BackgroundLayoutId} not found.");

        if (request.SubjectAssetId.HasValue)
        {
            var assetExists = await _db.SubjectAssets
                .AnyAsync(a => a.Id == request.SubjectAssetId.Value, ct);
            if (!assetExists)
                return NotFound($"SubjectAsset {request.SubjectAssetId.Value} not found.");
        }

        var project = new DesignProject
        {
            BackgroundLayoutId = request.BackgroundLayoutId,
            SubjectAssetId     = request.SubjectAssetId,
            ProductType        = request.ProductType,
            OccasionType       = request.OccasionType,
            TextConfigJson     = JsonSerializer.Serialize(new { title = "", subtitle = "", footer = "" }),
        };

        _db.DesignProjects.Add(project);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = project.Id }, ToDetail(project));
    }

    // ── GET /api/projects/{id} ────────────────────────────────────────────────

    /// <summary>Returns the full project detail including parsed JSON fields.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDetail>> Get(Guid id, CancellationToken ct)
    {
        var project = await _db.DesignProjects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (project is null)
            return NotFound($"Project {id} not found.");

        return Ok(ToDetail(project));
    }

    // ── PATCH /api/projects/{id}/text ─────────────────────────────────────────

    /// <summary>
    /// Replaces the project's text content (title, subtitle, footer).
    /// Validates field lengths: Title ≤ 40, Subtitle ≤ 60, Footer ≤ 120.
    /// </summary>
    [HttpPatch("{id:guid}/text")]
    public async Task<ActionResult<ProjectDetail>> UpdateText(
        Guid id,
        [FromBody] UpdateTextRequest request,
        CancellationToken ct)
    {
        if ((request.Title?.Length ?? 0) > 40)
            return ValidationProblem("Title must be 40 characters or fewer.");
        if ((request.Subtitle?.Length ?? 0) > 60)
            return ValidationProblem("Subtitle must be 60 characters or fewer.");
        if ((request.Footer?.Length ?? 0) > 120)
            return ValidationProblem("Footer must be 120 characters or fewer.");

        var project = await _db.DesignProjects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null)
            return NotFound($"Project {id} not found.");

        project.TextConfigJson = JsonSerializer.Serialize(new
        {
            title    = request.Title    ?? "",
            subtitle = request.Subtitle ?? "",
            footer   = request.Footer   ?? "",
        });
        project.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(ToDetail(project));
    }

    // ── PATCH /api/projects/{id}/crop ─────────────────────────────────────────

    /// <summary>
    /// Replaces the project's subject crop state.
    /// Each entry must have a non-empty cropFrameId, offsetX/Y in [-1, 1], scale in [0.1, 10].
    /// Send an empty array to clear crop state.
    /// </summary>
    [HttpPatch("{id:guid}/crop")]
    public async Task<ActionResult<ProjectDetail>> UpdateCrop(
        Guid id,
        [FromBody] UpdateCropRequest request,
        CancellationToken ct)
    {
        if (request.CropStates is null)
            return ValidationProblem("cropStates must be an array (may be empty).");

        for (int i = 0; i < request.CropStates.Count; i++)
        {
            var entry = request.CropStates[i];
            if (string.IsNullOrWhiteSpace(entry.CropFrameId))
                return ValidationProblem($"cropStates[{i}].cropFrameId must be a non-empty string.");
            if (entry.OffsetX < -1.0 || entry.OffsetX > 1.0)
                return ValidationProblem($"cropStates[{i}].offsetX must be in [-1.0, 1.0].");
            if (entry.OffsetY < -1.0 || entry.OffsetY > 1.0)
                return ValidationProblem($"cropStates[{i}].offsetY must be in [-1.0, 1.0].");
            if (entry.Scale < 0.1 || entry.Scale > 10.0)
                return ValidationProblem($"cropStates[{i}].scale must be in [0.1, 10.0].");
        }

        var project = await _db.DesignProjects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null)
            return NotFound($"Project {id} not found.");

        project.SubjectCropStateJson = request.CropStates.Count > 0
            ? JsonSerializer.Serialize(request.CropStates)
            : null;
        project.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(ToDetail(project));
    }

    // ── PATCH /api/projects/{id}/adjustments ──────────────────────────────────

    /// <summary>
    /// Replaces the project's canvas layout (per-layer placement transforms).
    /// All five layer keys (background, subject, title, subtitle, footer) are required.
    /// scale must be in [0.01, 20], rotation in [-360, 360].
    /// </summary>
    [HttpPatch("{id:guid}/adjustments")]
    public async Task<ActionResult<ProjectDetail>> UpdateAdjustments(
        Guid id,
        [FromBody] UpdateAdjustmentsRequest request,
        CancellationToken ct)
    {
        if (request.Layout is null)
            return ValidationProblem("layout is required.");

        var layers = new (string Name, LayerTransformDto? Layer)[]
        {
            ("background", request.Layout.Background),
            ("subject",    request.Layout.Subject),
            ("title",      request.Layout.Title),
            ("subtitle",   request.Layout.Subtitle),
            ("footer",     request.Layout.Footer),
        };

        foreach (var (name, layer) in layers)
        {
            if (layer is null)
                return ValidationProblem($"layout.{name} is required.");
            if (layer.Scale < 0.01 || layer.Scale > 20.0)
                return ValidationProblem($"layout.{name}.scale must be in [0.01, 20.0].");
            if (layer.Rotation < -360.0 || layer.Rotation > 360.0)
                return ValidationProblem($"layout.{name}.rotation must be in [-360, 360].");
        }

        var project = await _db.DesignProjects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null)
            return NotFound($"Project {id} not found.");

        project.UserAdjustmentsJson = JsonSerializer.Serialize(request.Layout, _jsonOpts);
        project.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(ToDetail(project));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ProjectDetail ToDetail(DesignProject p) => new(
        p.Id,
        p.BackgroundLayoutId,
        p.SubjectAssetId,
        p.ProductType.ToString(),
        p.OccasionType.ToString(),
        ParseTextConfig(p.TextConfigJson),
        ParseCanvasLayout(p.UserAdjustmentsJson),
        ParseCropStates(p.SubjectCropStateJson),
        p.Status.ToString(),
        p.CreatedAt,
        p.UpdatedAt);

    private static TextConfigDto ParseTextConfig(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new TextConfigDto("", "", "");
        try
        {
            var raw = JsonSerializer.Deserialize<TextConfigRaw>(json, _jsonOpts);
            return new TextConfigDto(
                raw?.Title    ?? "",
                raw?.Subtitle ?? "",
                raw?.Footer   ?? "");
        }
        catch (JsonException) { return new TextConfigDto("", "", ""); }
    }

    private static CanvasLayoutDto? ParseCanvasLayout(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<CanvasLayoutDto>(json, _jsonOpts); }
        catch (JsonException) { return null; }
    }

    private static IReadOnlyList<CropStateDto>? ParseCropStates(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var list = JsonSerializer.Deserialize<List<CropStateDto>>(json, _jsonOpts);
            return list is { Count: > 0 } ? list : null;
        }
        catch (JsonException) { return null; }
    }

    // Internal raw deserialisation helpers
    private sealed class TextConfigRaw
    {
        public string? Title    { get; set; }
        public string? Subtitle { get; set; }
        public string? Footer   { get; set; }
    }
}
