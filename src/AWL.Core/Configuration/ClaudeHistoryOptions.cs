namespace AWL.Core.Configuration;

/// <summary>Configures additional Claude Code paths associated with an individual session.</summary>
public sealed class ClaudeHistoryOptions
{
    /// <summary>Gets the explicitly configured extra session paths.</summary>
    public string[] ExtraSessionPaths { get; init; } = [];
}
