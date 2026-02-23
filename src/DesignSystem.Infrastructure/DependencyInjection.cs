using DesignSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DesignSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("AppDb") ?? "Data Source=./data/app.db";

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlite(conn);
        });

        return services;
    }
}
