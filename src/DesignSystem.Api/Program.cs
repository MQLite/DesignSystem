using DesignSystem.Infrastructure;
using DesignSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(p =>
        p.WithOrigins("http://localhost:5173")
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
});
app.UseHttpsRedirection();
app.MapControllers();
app.Run();
