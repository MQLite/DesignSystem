using DesignSystem.Infrastructure.Persistence;
using DesignSystem.Infrastructure.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DesignSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("AppDb")
            ?? "Server=localhost;Database=DesignSystem;Trusted_Connection=True;TrustServerCertificate=True;";

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(conn);
        });

        // Composer engine — scoped so it shares the request lifetime with DbContext
        services.AddScoped<IComposerEngine, ComposerEngine>();

        return services;
    }
}
