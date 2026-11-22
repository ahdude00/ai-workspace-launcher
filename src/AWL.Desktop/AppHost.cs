using AWL.Core;
using AWL.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AWL.Desktop;

/// <summary>
/// Builds the generic host. The only place that knows about every layer.
/// </summary>
internal static class AppHost
{
    public static IHost Build(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });

        // File logging (Serilog) arrives with F1-05; until then no console/debug providers.
        builder.Logging.ClearProviders();

        builder.Services
            .AddCore()
            .AddInfrastructure()
            .AddDesktop();

        return builder.Build();
    }
}
