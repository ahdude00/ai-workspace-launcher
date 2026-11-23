using AWL.Core.Categories;
using AWL.Core.Claude;
using AWL.Core.Configuration;
using AWL.Core.Sessions;
using AWL.Infrastructure.Claude;
using AWL.Infrastructure.Configuration;
using AWL.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AWL.Infrastructure;

/// <summary>
/// Registers the Windows-specific implementations of Core interfaces.
/// </summary>
public static class InfrastructureModule
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IClaudeInstallation>(provider => new PowerShellClaudeInstaller(
            provider.GetRequiredService<AwlPaths>(), provider.GetRequiredService<ILogger<PowerShellClaudeInstaller>>(),
            ClaudeProcess.RunAsync, () => Environment.GetEnvironmentVariable("PATH")));
        services.TryAddSingleton<IAwlSettingsStore, JsonAwlSettingsStore>();
        services.TryAddSingleton<SqliteDatabase>();
        services.TryAddSingleton<ISessionRepository, SqliteSessionRepository>();
        services.TryAddSingleton<ICategoryRepository, SqliteCategoryRepository>();
        return services;
    }
}
