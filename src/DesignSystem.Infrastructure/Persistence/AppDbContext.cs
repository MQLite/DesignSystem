using DesignSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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

        modelBuilder.Entity<BackgroundLayout>()
            .Property(l => l.SubjectSlotsJson)
            .HasColumnType("TEXT");

        modelBuilder.Entity<BackgroundLayout>()
            .Property(l => l.SubjectCropFramesJson)
            .HasColumnType("TEXT");

        modelBuilder.Entity<BackgroundLayout>()
            .Property(l => l.TextZonesJson)
            .HasColumnType("TEXT");

        modelBuilder.Entity<DesignProject>()
            .Property(p => p.TextConfigJson)
            .HasColumnType("TEXT");

        modelBuilder.Entity<DesignProject>()
            .Property(p => p.UserAdjustmentsJson)
            .HasColumnType("TEXT");

        modelBuilder.Entity<DesignProject>()
            .Property(p => p.SubjectCropStateJson)
            .HasColumnType("TEXT");

        // SQLite does not support DateTimeOffset in ORDER BY.
        // Store all DateTimeOffset values as long (Unix ms) so sorting works natively.
        var dateTimeOffsetConverter = new ValueConverter<DateTimeOffset, long>(
            v => v.ToUnixTimeMilliseconds(),
            v => DateTimeOffset.FromUnixTimeMilliseconds(v));

        var nullableDateTimeOffsetConverter = new ValueConverter<DateTimeOffset?, long?>(
            v => v == null ? null : v.Value.ToUnixTimeMilliseconds(),
            v => v == null ? null : DateTimeOffset.FromUnixTimeMilliseconds(v.Value));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                    property.SetValueConverter(dateTimeOffsetConverter);
                else if (property.ClrType == typeof(DateTimeOffset?))
                    property.SetValueConverter(nullableDateTimeOffsetConverter);
            }
        }

        base.OnModelCreating(modelBuilder);
    }
}
