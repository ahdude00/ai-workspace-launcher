using AWL.Core.Configuration;
using AWL.Infrastructure.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AWL.Infrastructure.Tests.Diagnostics;

public sealed class FileLoggingTests : IDisposable
{
    private static readonly Action<ILogger, string, Exception?> _writeInformation = LoggerMessage.Define<string>(
        LogLevel.Information, new EventId(1, "TestInformation"), "Diagnostic event {Text}");
    private static readonly Action<ILogger, string, Exception?> _writeDebug = LoggerMessage.Define<string>(
        LogLevel.Debug, new EventId(2, "TestDebug"), "Debug event {Text}");
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("awl-log-tests-");

    [Fact]
    public void Writes_awl_events_without_host_messages_exception_text_or_scopes()
    {
        AwlPaths paths = CreatePaths();
        using (ILoggerProvider provider = FileLogging.CreateProvider(paths, new LoggingOptions()))
        {
            ILogger logger = provider.CreateLogger("AWL.Test");
            using IDisposable? scope = logger.BeginScope("private-scope-marker");
            _writeInformation(logger, "allowed-marker", new InvalidOperationException("private-error-marker"));
            _writeInformation(provider.CreateLogger("Microsoft.Hosting.Lifetime"), "host-path-marker", null);
            _writeDebug(logger, "debug-marker", null);
        }

        string content = File.ReadAllText(Path.Combine(paths.LogsDirectory, "awl.log"));
        content.ShouldContain("allowed-marker");
        content.ShouldContain("debug-marker");
        content.ShouldNotContain("private-scope-marker");
        content.ShouldNotContain("private-error-marker");
        content.ShouldNotContain("host-path-marker");
    }

    [Fact]
    public void Rolls_files_and_keeps_only_the_configured_count()
    {
        AwlPaths paths = CreatePaths();
        var options = new LoggingOptions { FileSizeLimitBytes = 128, RetainedFileCountLimit = 2 };
        string payload = new('x', 256);
        using (ILoggerProvider provider = FileLogging.CreateProvider(paths, options))
        {
            ILogger logger = provider.CreateLogger("AWL.Test");
            for (int index = 0; index < 6; index++)
            {
                _writeInformation(logger, $"Entry {index} {payload}", null);
            }
        }

        string[] files = Directory.GetFiles(paths.LogsDirectory, "*.log");
        files.Length.ShouldBe(2);
        string content = string.Join(Environment.NewLine, files.Select(File.ReadAllText));
        content.ShouldContain("Entry 5");
        content.ShouldNotContain("Entry 0");
    }

    public void Dispose() => _directory.Delete(recursive: true);

    private AwlPaths CreatePaths() => new(_directory.FullName, _directory.FullName, _directory.FullName);
}
