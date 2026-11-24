using AWL.Core;
using AWL.Core.Configuration;
using AWL.Infrastructure;
using AWL.Infrastructure.Diagnostics;
using Microsoft.Extensions.Configuration;
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

        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Services.AddSingleton<ILoggerProvider>(services => FileLogging.CreateProvider(
            services.GetRequiredService<AwlPaths>(), new LoggingOptions()));

        builder.Services.AddSingleton(builder.Configuration.GetSection("ClaudeHistory").Get<ClaudeHistoryOptions>()
            ?? new ClaudeHistoryOptions());
        builder.Services
            .AddCore()
            .AddInfrastructure()
            .AddDesktop();

        return builder.Build();
    }
}
