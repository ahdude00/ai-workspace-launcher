using AWL.Core.Common;

namespace AWL.Core.Claude;

public interface IClaudeProcessMonitor
{
    IReadOnlySet<Guid> RunningSessionIds { get; }

    event EventHandler? Changed;

    Task<Result> StopSessionAsync(Guid sessionId, CancellationToken token);
}
