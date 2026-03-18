using DesignSystem.Domain.Entities;
using DesignSystem.Domain.Enums;
using DesignSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DesignSystem.Api.Features.Admin;

// ── Response types ─────────────────────────────────────────────────────────────

public record AdminLayoutDetail(
    Guid Id,
    string SizeCode,
    int WidthMm,
    int HeightMm,
    string Orientation,
    string SubjectSlotsJson,
    string? SubjectCropFramesJson,
    string? TextZonesJson,
    int Version);

public record AdminBackgroundDetail(
    Guid Id,
    string Name,
    string OccasionType,
    string? PreviewPath,
    string? SourcePath,
    IReadOnlyList<AdminLayoutDetail> Layouts);

// ── Request types ───────────────────────────────────────────────────────────────

public record CreateAdminBackgroundRequest(string Name, string OccasionType);
public record UpdateAdminBackgroundRequest(string Name, string OccasionType);

public record CreateAdminLayoutRequest(
    string SizeCode,
    int WidthMm,
    int HeightMm,
    string Orientation,
    string SubjectSlotsJson,
    string? SubjectCropFramesJson,
    string? TextZonesJson);

public record UpdateAdminLayoutRequest(
    string SizeCode,
    int WidthMm,
    int HeightMm,
    string Orientation,
    string SubjectSlotsJson,
    string? SubjectCropFramesJson,
    string? TextZonesJson);

// ── Controller ─────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/admin/backgrounds")]
public sealed class AdminBackgroundsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public AdminBackgroundsController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // ── GET /api/admin/backgrounds ─────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminBackgroundDetail>>> GetAll(CancellationToken ct)
    {
        var items = await _db.Backgrounds
            .AsNoTracking()
            .Include(b => b.Layouts)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);

        return Ok(items.Select(ToDetail).ToList());
    }

    // ── GET /api/admin/backgrounds/{id} ───────────────────────────────────────

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminBackgroundDetail>> GetOne(Guid id, CancellationToken ct)
    {
        var bg = await _db.Backgrounds.AsNoTracking()
            .Include(b => b.Layouts)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        if (bg is null) return NotFound($"Background {id} not found.");
        return Ok(ToDetail(bg));
    }

    // ── POST /api/admin/backgrounds ───────────────────────────────────────────

    [HttpPost]
    public async Task<ActionResult<AdminBackgroundDetail>> Create(
        [FromBody] CreateAdminBackgroundRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ValidationProblem("Name is required.");

        if (!Enum.TryParse<OccasionType>(request.OccasionType, out var occasion))
            return ValidationProblem($"Invalid OccasionType: {request.OccasionType}");

        var bg = new Background
        {
            Name = request.Name.Trim(),
            OccasionType = occasion,
        };

        _db.Backgrounds.Add(bg);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetOne), new { id = bg.Id }, ToDetail(bg));
    }

    // ── POST /api/admin/backgrounds/{id}/image ────────────────────────────────

    /// <summary>
    /// Uploads the source image for a background.
    /// For PoC the same file is used as both source and preview.
    /// </summary>
    [HttpPost("{id:guid}/image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<AdminBackgroundDetail>> UploadImage(
        Guid id,
        IFormFile file,
        CancellationToken ct)
    {
        var bg = await _db.Backgrounds.Include(b => b.Layouts)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        if (bg is null) return NotFound($"Background {id} not found.");
        if (file is null || file.Length == 0) return BadRequest("File is required.");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".png";

        var storageRoot = Path.Combine(_env.ContentRootPath, "storage", "backgrounds");
        Directory.CreateDirectory(storageRoot);

        var sourceName  = $"{id}_source{ext}";
        var previewName = $"{id}_preview{ext}";
        var absSource   = Path.Combine(storageRoot, sourceName);
        var absPreview  = Path.Combine(storageRoot, previewName);

        await using (var stream = System.IO.File.Create(absSource))
            await file.CopyToAsync(stream, ct);

        // PoC: copy source as preview (no thumbnail generation)
        System.IO.File.Copy(absSource, absPreview, overwrite: true);

        bg.SourcePath  = $"storage/backgrounds/{sourceName}";
        bg.PreviewPath = $"storage/backgrounds/{previewName}";
        await _db.SaveChangesAsync(ct);

        return Ok(ToDetail(bg));
    }

    // ── PATCH /api/admin/backgrounds/{id} ─────────────────────────────────────

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<AdminBackgroundDetail>> Update(
        Guid id,
        [FromBody] UpdateAdminBackgroundRequest request,
        CancellationToken ct)
    {
        var bg = await _db.Backgrounds.Include(b => b.Layouts)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        if (bg is null) return NotFound($"Background {id} not found.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return ValidationProblem("Name is required.");

        if (!Enum.TryParse<OccasionType>(request.OccasionType, out var occasion))
            return ValidationProblem($"Invalid OccasionType: {request.OccasionType}");

        bg.Name        = request.Name.Trim();
        bg.OccasionType = occasion;
        await _db.SaveChangesAsync(ct);

        return Ok(ToDetail(bg));
    }

    // ── DELETE /api/admin/backgrounds/{id} ────────────────────────────────────

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var bg = await _db.Backgrounds.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (bg is null) return NotFound($"Background {id} not found.");

        _db.Backgrounds.Remove(bg);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    // ── POST /api/admin/backgrounds/{id}/layouts ──────────────────────────────

    [HttpPost("{id:guid}/layouts")]
    public async Task<ActionResult<AdminLayoutDetail>> CreateLayout(
        Guid id,
        [FromBody] CreateAdminLayoutRequest request,
        CancellationToken ct)
    {
        var bgExists = await _db.Backgrounds.AnyAsync(b => b.Id == id, ct);
        if (!bgExists) return NotFound($"Background {id} not found.");

        var layout = new BackgroundLayout
        {
            BackgroundId          = id,
            SizeCode              = request.SizeCode,
            WidthMm               = request.WidthMm,
            HeightMm              = request.HeightMm,
            Orientation           = request.Orientation,
            SubjectSlotsJson      = request.SubjectSlotsJson,
            SubjectCropFramesJson = request.SubjectCropFramesJson,
            TextZonesJson         = request.TextZonesJson,
        };

        _db.BackgroundLayouts.Add(layout);
        await _db.SaveChangesAsync(ct);

        return Ok(ToLayoutDetail(layout));
    }

    // ── PATCH /api/admin/backgrounds/{id}/layouts/{layoutId} ──────────────────

    [HttpPatch("{id:guid}/layouts/{layoutId:guid}")]
    public async Task<ActionResult<AdminLayoutDetail>> UpdateLayout(
        Guid id,
        Guid layoutId,
        [FromBody] UpdateAdminLayoutRequest request,
        CancellationToken ct)
    {
        var layout = await _db.BackgroundLayouts
            .FirstOrDefaultAsync(l => l.Id == layoutId && l.BackgroundId == id, ct);

        if (layout is null) return NotFound($"Layout {layoutId} not found.");

        layout.SizeCode              = request.SizeCode;
        layout.WidthMm               = request.WidthMm;
        layout.HeightMm              = request.HeightMm;
        layout.Orientation           = request.Orientation;
        layout.SubjectSlotsJson      = request.SubjectSlotsJson;
        layout.SubjectCropFramesJson = request.SubjectCropFramesJson;
        layout.TextZonesJson         = request.TextZonesJson;
        layout.Version++;

        await _db.SaveChangesAsync(ct);

        return Ok(ToLayoutDetail(layout));
    }

    // ── DELETE /api/admin/backgrounds/{id}/layouts/{layoutId} ─────────────────

    [HttpDelete("{id:guid}/layouts/{layoutId:guid}")]
    public async Task<IActionResult> DeleteLayout(
        Guid id,
        Guid layoutId,
        CancellationToken ct)
    {
        var layout = await _db.BackgroundLayouts
            .FirstOrDefaultAsync(l => l.Id == layoutId && l.BackgroundId == id, ct);

        if (layout is null) return NotFound($"Layout {layoutId} not found.");

        _db.BackgroundLayouts.Remove(layout);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AdminBackgroundDetail ToDetail(Background bg) => new(
        bg.Id,
        bg.Name,
        bg.OccasionType.ToString(),
        bg.PreviewPath,
        bg.SourcePath,
        bg.Layouts.Select(ToLayoutDetail).ToList());

    private static AdminLayoutDetail ToLayoutDetail(BackgroundLayout l) => new(
        l.Id,
        l.SizeCode,
        l.WidthMm,
        l.HeightMm,
        l.Orientation,
        l.SubjectSlotsJson,
        l.SubjectCropFramesJson,
        l.TextZonesJson,
        l.Version);
}
