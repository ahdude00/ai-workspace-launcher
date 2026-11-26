using AWL.Core.Common;

namespace AWL.Core.Claude;

public interface IClaudeProcessMonitor
{
    IReadOnlySet<Guid> RunningSessionIds { get; }

    event EventHandler? Changed;

    Task<Result> StopSessionAsync(Guid sessionId, CancellationToken token);

    /// <summary>Tries to bring the terminal window of a running session to the foreground.</summary>
    /// <returns>Whether a window was found and activated.</returns>
    Task<bool> TryBringToFrontAsync(Guid sessionId, CancellationToken token);
}
