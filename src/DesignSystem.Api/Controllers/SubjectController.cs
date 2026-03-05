using DesignSystem.Domain.Entities;
using DesignSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace DesignSystem.Api.Controllers;

public sealed class UploadSubjectRequest
{
    public IFormFile File { get; set; } = default!;
}

[ApiController]
[Route("api/subject")]
public sealed class SubjectController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public SubjectController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Upload([FromForm] UploadSubjectRequest request, CancellationToken ct)
    {
        var file = request.File;

        if (file is null || file.Length == 0)
            return BadRequest("File is required.");

        var id = Guid.NewGuid();
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

        var relPath = $"storage/uploads/{id}{ext}";
        var absPath = Path.Combine(_env.ContentRootPath, "storage", "uploads", $"{id}{ext}");

        await using (var stream = System.IO.File.Create(absPath))
        {
            await file.CopyToAsync(stream, ct);
        }

        var asset = new SubjectAsset
        {
            Id = id,
            OriginalPath = relPath
        };

        _db.SubjectAssets.Add(asset);
        await _db.SaveChangesAsync(ct);

        return Ok(new { subjectAssetId = asset.Id, originalPath = asset.OriginalPath });
    }
}
