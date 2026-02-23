using DesignSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DesignSystem.Api.Controllers;

[ApiController]
[Route("api/backgrounds")]
public sealed class BackgroundsController : ControllerBase
{
    private readonly AppDbContext _db;

    public BackgroundsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var items = await _db.Backgrounds
            .AsNoTracking()
            .Include(b => b.Layouts)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new
            {
                b.Id,
                b.Name,
                b.OccasionType,
                b.PreviewPath,
                b.SourcePath,
                Layout = b.Layouts.Select(l => new
                {
                    l.Id,
                    l.SizeCode,
                    l.Orientation,
                    l.SubjectSlotsJson,
                    l.TextZonesJson,
                    l.Version
                })
            })
            .ToListAsync(ct);

        return Ok(items);
    }
}
