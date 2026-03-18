using DesignSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DesignSystem.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Background> Backgrounds => Set<Background>();
    public DbSet<BackgroundLayout> BackgroundLayouts => Set<BackgroundLayout>();
    public DbSet<SubjectAsset> SubjectAssets => Set<SubjectAsset>();
    public DbSet<DesignProject> DesignProjects => Set<DesignProject>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Background>()
            .HasMany(b => b.Layouts)
            .WithOne(l => l.Background!)
            .HasForeignKey(l => l.BackgroundId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Background>()
            .HasIndex(b => b.OccasionType);

        modelBuilder.Entity<DesignProject>()
            .HasIndex(p => p.Status);

        modelBuilder.Entity<DesignProject>()
            .HasIndex(p => p.CreatedAt);

        // Store JSON blobs as nvarchar(max) — SQL Server native type for large text.
        modelBuilder.Entity<BackgroundLayout>()
            .Property(l => l.SubjectSlotsJson)
            .HasColumnType("nvarchar(max)");

        modelBuilder.Entity<BackgroundLayout>()
            .Property(l => l.SubjectCropFramesJson)
            .HasColumnType("nvarchar(max)");

        modelBuilder.Entity<BackgroundLayout>()
            .Property(l => l.TextZonesJson)
            .HasColumnType("nvarchar(max)");

        modelBuilder.Entity<DesignProject>()
            .Property(p => p.TextConfigJson)
            .HasColumnType("nvarchar(max)");

        modelBuilder.Entity<DesignProject>()
            .Property(p => p.UserAdjustmentsJson)
            .HasColumnType("nvarchar(max)");

        modelBuilder.Entity<DesignProject>()
            .Property(p => p.SubjectCropStateJson)
            .HasColumnType("nvarchar(max)");

        base.OnModelCreating(modelBuilder);
    }
}
