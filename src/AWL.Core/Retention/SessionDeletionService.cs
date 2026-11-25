using AWL.Core.Claude;
using AWL.Core.Common;
using AWL.Core.Configuration;
using AWL.Core.Sessions;

namespace AWL.Core.Retention;

/// <summary>
/// Deletes a session from AWL and from Claude together, never leaving files without an owner (D18, OTR-11–14, OTR-16):
/// Claude's files go first; the AWL record is removed only after they are all gone.
/// </summary>
public sealed class SessionDeletionService(ISessionRepository repository, IClaudeHistoryStore history,
    IClaudeProcessMonitor monitor, IAwlSettingsStore settings, IClock clock)
{
    /// <summary>Stored as <see cref="Session.LastErrorCode"/> when Claude's files could not all be deleted.</summary>
    public const string DeleteIncompleteCode = "DeleteIncomplete";

    /// <summary>How long to wait after stopping Claude before retrying files it may still have held open.</summary>
    public TimeSpan SettleDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Raised after a session was deleted or marked as failed.</summary>
    public event EventHandler? Changed;

    public bool IsRunning(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return monitor.RunningSessionIds.Contains(session.ClaudeSessionId);
    }

    /// <summary>
    /// Deletes the session. An open session is stopped only when <paramref name="stopIfRunning"/> is set (OTR-13);
    /// otherwise <see cref="Errors.SessionOpen"/> is returned. If some Claude files remain, the record is kept in the
    /// "Error" state and the deletion can be retried (OTR-14).
    /// </summary>
    public async Task<Result> DeleteAsync(Guid sessionId, bool stopIfRunning, CancellationToken cancellationToken = default)
    {
        var session = await repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return Result.Fail(Errors.SessionNotFound);
        }

        bool stopped = false;
        if (IsRunning(session))
        {
            if (!stopIfRunning)
            {
                return Result.Fail(Errors.SessionOpen);
            }
            var stop = await monitor.StopSessionAsync(session.ClaudeSessionId, cancellationToken).ConfigureAwait(false);
            if (!stop.IsSuccess)
            {
                return stop;
            }
            stopped = true;
        }

        var report = await history.DeleteAsync(session.ClaudeSessionId, cancellationToken).ConfigureAwait(false);
        if (!report.IsComplete && stopped)
        {
            // A process that was just stopped can hold its transcript open for a moment.
            await Task.Delay(SettleDelay, cancellationToken).ConfigureAwait(false);
            report = await history.DeleteAsync(session.ClaudeSessionId, cancellationToken).ConfigureAwait(false);
        }

        if (!report.IsComplete)
        {
            await repository.UpdateAsync(session with { LastErrorCode = DeleteIncompleteCode }, cancellationToken)
                .ConfigureAwait(false);
            RaiseChanged();
            return Result.Fail(Errors.DeleteIncomplete with { Detail = $"{report.Failures.Count} path(s) remain" });
        }

        await repository.DeleteAsync(session.Id, cancellationToken).ConfigureAwait(false);
        RaiseChanged();
        return Result.Ok();
    }

    /// <summary>
    /// Startup clean-up (OTR-16): deletes every session that has outlived the retention period and is not open.
    /// </summary>
    /// <returns>The number of sessions deleted.</returns>
    public async Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        int days = settings.Load().RetentionDays;
        if (days < RetentionPolicy.MinDays)
        {
            return 0;
        }
        DateTimeOffset now = clock.UtcNow;
        var sessions = await repository.ListAsync(cancellationToken).ConfigureAwait(false);
        int deleted = 0;
        foreach (var session in sessions.Where(session => !IsRunning(session) && RetentionPolicy.IsExpired(session, days, now)))
        {
            if ((await DeleteAsync(session.Id, stopIfRunning: false, cancellationToken).ConfigureAwait(false)).IsSuccess)
            {
                deleted++;
            }
        }
        return deleted;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>Error codes returned by <see cref="SessionDeletionService"/>; each code is also its resource key.</summary>
    public static class Errors
    {
        public static readonly AppError SessionNotFound = new("SessionNotFound", "SessionNotFound");
        public static readonly AppError SessionOpen = new("SessionOpen", "SessionOpen");
        public static readonly AppError DeleteIncomplete = new(DeleteIncompleteCode, DeleteIncompleteCode);
    }
}
