namespace AWL.Core.Configuration;

/// <summary>Configures additional Claude Code paths associated with an individual session.</summary>
public sealed class ClaudeHistoryOptions
{
    /// <summary>Relative paths beneath the Claude configuration directory. Use {uuid} as a directory name or filename stem.</summary>
    public string[] ExtraSessionPaths { get; init; } = [];
}
