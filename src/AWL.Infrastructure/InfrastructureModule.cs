using AWL.Core.Categories;
using AWL.Core.Claude;
using AWL.Core.Configuration;
using AWL.Core.Python;
using AWL.Core.Sessions;
using AWL.Core.Setup;
using AWL.Infrastructure.Setup;
using AWL.Infrastructure.Claude;
using AWL.Infrastructure.Configuration;
using AWL.Infrastructure.Persistence;
using AWL.Infrastructure.Processes;
using AWL.Infrastructure.Python;
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
        services.TryAddSingleton<IPythonEnvironment>(provider => new UvPythonEnvironment(
            provider.GetRequiredService<AwlPaths>(), provider.GetRequiredService<ILogger<UvPythonEnvironment>>(),
            ChildProcess.RunAsync));
        services.TryAddSingleton<IClaudeInstallation>(provider => new PowerShellClaudeInstaller(
            provider.GetRequiredService<AwlPaths>(), provider.GetRequiredService<ILogger<PowerShellClaudeInstaller>>(),
            ChildProcess.RunAsync, () => Environment.GetEnvironmentVariable("PATH")));
        services.TryAddSingleton<IAwlSettingsStore, JsonAwlSettingsStore>();
        services.TryAddSingleton<SqliteDatabase>();
        services.TryAddSingleton<ISessionRepository, SqliteSessionRepository>();
        services.TryAddSingleton<ICategoryRepository, SqliteCategoryRepository>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISetupStep, PreflightStep>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISetupStep, ClaudeStep>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISetupStep, PythonStep>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISetupStep, ConfigurationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISetupStep, UdfStep>());
        services.TryAddSingleton<SetupPipeline>();
        return services;
    }
}
