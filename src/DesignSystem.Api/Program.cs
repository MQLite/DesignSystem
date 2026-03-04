using DesignSystem.Infrastructure;
using DesignSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

Directory.CreateDirectory("./data");
Directory.CreateDirectory("./storage");
Directory.CreateDirectory("./storage/backgrounds");
Directory.CreateDirectory("./storage/uploads");
Directory.CreateDirectory("./storage/processed");
Directory.CreateDirectory("./storage/previews");
Directory.CreateDirectory("./storage/exports");

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

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
