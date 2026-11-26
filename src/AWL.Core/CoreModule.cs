using AWL.Core.Categories;
using AWL.Core.Claude;
using AWL.Core.Common;
using AWL.Core.Configuration;
using AWL.Core.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AWL.Core;

/// <summary>
/// Registers the services of every Core module (Sessions, Categories, Setup, …).
/// Each module adds its own registrations here as it is implemented.
/// </summary>
public static class CoreModule
{
    /// <summary>Registers shared Core services and returns the same service collection.</summary>
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ClaudeCommandBuilder>();
        services.TryAddSingleton<SessionService>();
        services.TryAddSingleton<CategoryService>();
        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddSingleton<AwlPaths>(_ => AwlPaths.FromEnvironment());
        return services;
    }
}
