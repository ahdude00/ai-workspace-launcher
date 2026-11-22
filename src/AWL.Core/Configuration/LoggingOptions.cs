namespace AWL.Core.Configuration;

/// <summary>Defines the size and retention limits for AWL log files.</summary>
public sealed class LoggingOptions
{
    /// <summary>Gets the maximum size of each log file in bytes.</summary>
    public long FileSizeLimitBytes { get; init; } = 5 * 1024 * 1024;

    /// <summary>Gets the maximum number of retained log files.</summary>
    public int RetainedFileCountLimit { get; init; } = 10;
}
