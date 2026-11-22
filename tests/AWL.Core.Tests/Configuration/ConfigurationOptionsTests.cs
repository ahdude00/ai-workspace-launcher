using AWL.Core.Configuration;

namespace AWL.Core.Tests.Configuration;

public sealed class ConfigurationOptionsTests
{
    [Fact]
    public void Logging_defaults_to_ten_files_of_five_megabytes()
    {
        var options = new LoggingOptions();

        options.FileSizeLimitBytes.ShouldBe(5L * 1024 * 1024);
        options.RetainedFileCountLimit.ShouldBe(10);
    }

    [Fact]
    public void Claude_history_defaults_to_no_extra_session_paths()
    {
        var options = new ClaudeHistoryOptions();

        options.ExtraSessionPaths.ShouldBeEmpty();
    }
}
