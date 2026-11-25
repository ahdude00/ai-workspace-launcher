using AWL.Core.Common;

namespace AWL.Core.Claude;

public interface IClaudeInstallation
{
    /// <summary>The executable verified during startup, if verification succeeded.</summary>
    string? ExecutablePath { get; }

    /// <summary>The Claude Code version reported during the last successful verification.</summary>
    string? Version { get; }

    Task<bool> CheckAsync(CancellationToken cancellationToken);

    Task<Result<string>> EnsureInstalledAsync(CancellationToken cancellationToken);
}
