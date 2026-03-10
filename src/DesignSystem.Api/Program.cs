using DesignSystem.Infrastructure;
using DesignSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(p =>
        p.WithOrigins(allowedOrigins)
         .AllowAnyHeader()
         .AllowAnyMethod()));

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

var root = app.Environment.ContentRootPath;
Directory.CreateDirectory(Path.Combine(root, "data"));
Directory.CreateDirectory(Path.Combine(root, "storage"));
Directory.CreateDirectory(Path.Combine(root, "storage", "backgrounds"));
Directory.CreateDirectory(Path.Combine(root, "storage", "uploads"));
Directory.CreateDirectory(Path.Combine(root, "storage", "processed"));
Directory.CreateDirectory(Path.Combine(root, "storage", "previews"));
Directory.CreateDirectory(Path.Combine(root, "storage", "exports"));

// PoC convenience: apply migrations and seed automatically on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await AppDbContextSeeder.SeedAsync(db);
}

// if (app.Environment.IsDevelopment())
// {
    app.UseSwagger();
    app.UseSwaggerUI();
// }

app.UseCors();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(app.Environment.ContentRootPath, "storage")),
    RequestPath = "/storage",
    OnPrepareResponse = ctx =>
    {
        // Ensure CORS headers are present on static file responses (needed for fetch() in SVG export)
        var origin = ctx.Context.Request.Headers.Origin.ToString();
        if (allowedOrigins.Contains(origin))
        {
            ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", origin);
            ctx.Context.Response.Headers.Append("Access-Control-Allow-Methods", "GET");
        }
    },
});
app.UseHttpsRedirection();
app.MapControllers();
app.Run();
