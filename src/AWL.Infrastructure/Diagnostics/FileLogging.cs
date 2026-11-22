using System.Globalization;
using AWL.Core.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace AWL.Infrastructure.Diagnostics;

/// <summary>Creates the rolling file provider for AWL application events.</summary>
public static class FileLogging
{
    /// <summary>Creates a provider that owns its file logger and supports opt-in Debug events.</summary>
    public static ILoggerProvider CreateProvider(AwlPaths paths, LoggingOptions options)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(options);

        // Keep raw exception and scope data out of the file, and omit host messages such as content roots.
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Filter.ByIncludingOnly(IsAwlEvent)
            .WriteTo.File(
                Path.Combine(paths.LogsDirectory, "awl.log"),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}",
                formatProvider: CultureInfo.InvariantCulture,
                fileSizeLimitBytes: options.FileSizeLimitBytes,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: options.RetainedFileCountLimit,
                shared: true)
            .CreateLogger();

        return new SerilogLoggerProvider(logger, dispose: true);
    }

    private static bool IsAwlEvent(LogEvent logEvent) =>
        logEvent.Properties.TryGetValue("SourceContext", out LogEventPropertyValue? source) &&
        source is ScalarValue { Value: string category } &&
        category.StartsWith("AWL.", StringComparison.Ordinal);
}
