using AWL.Core.Categories;
using AWL.Core.Configuration;
using AWL.Core.Sessions;
using AWL.Infrastructure.Configuration;
using AWL.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AWL.Infrastructure;

/// <summary>
/// Registers the Windows-specific implementations of Core interfaces.
/// </summary>
public static class InfrastructureModule
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IAwlSettingsStore, JsonAwlSettingsStore>();
        services.TryAddSingleton<SqliteDatabase>();
        services.TryAddSingleton<ISessionRepository, SqliteSessionRepository>();
        services.TryAddSingleton<ICategoryRepository, SqliteCategoryRepository>();
        return services;
    }
}
