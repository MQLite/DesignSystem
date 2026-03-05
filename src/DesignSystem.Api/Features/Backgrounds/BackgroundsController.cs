using DesignSystem.Domain.Enums;
using DesignSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DesignSystem.Api.Features.Backgrounds;

// ── Response types ──────────────────────────────────────────────────────────

public record LayoutSummary(
    Guid Id,
    string SizeCode,
    string Orientation,
    string SubjectSlotsJson,
    string? TextZonesJson,
    int Version);

public record BackgroundSummary(
    Guid Id,
    string Name,
    OccasionType OccasionType,
    string? PreviewPath,
    string? SourcePath,
    IReadOnlyList<LayoutSummary> Layout);

// ── Controller ───────────────────────────────────────────────────────────────

[ApiController]
[Route("api/backgrounds")]
public sealed class BackgroundsController : ControllerBase
{
    private readonly AppDbContext _db;

    public BackgroundsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BackgroundSummary>>> GetAll(CancellationToken ct)
    {
        var items = await _db.Backgrounds
            .AsNoTracking()
            .Include(b => b.Layouts)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BackgroundSummary(
                b.Id,
                b.Name,
                b.OccasionType,
                b.PreviewPath,
                b.SourcePath,
                b.Layouts.Select(l => new LayoutSummary(
                    l.Id,
                    l.SizeCode,
                    l.Orientation,
                    l.SubjectSlotsJson,
                    l.TextZonesJson,
                    l.Version)).ToList()))
            .ToListAsync(ct);

        return Ok(items);
    }
}
