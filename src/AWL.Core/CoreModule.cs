using Microsoft.Extensions.DependencyInjection;

namespace AWL.Core;

/// <summary>
/// Registers the services of every Core module (Sessions, Categories, Setup, …).
/// Each module adds its own registrations here as it is implemented.
/// </summary>
public static class CoreModule
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
