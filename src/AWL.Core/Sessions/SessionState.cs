namespace AWL.Core.Sessions;

/// <summary>The displayed state of a session. It is never stored; it is recalculated (D12).</summary>
public enum SessionState
{
    Closed,
    Open,
    Archived,
    HistoryMissing,
    Error,
}

/// <summary>Combines the stored session data with live observations into a <see cref="SessionState"/>.</summary>
public static class SessionStateResolver
{
    /// <summary>Resolves the state; an error wins over a running process, which wins over archiving.</summary>
    /// <param name="session">The stored session.</param>
    /// <param name="isRunning">Whether a Claude process carrying the session identifier is running.</param>
    /// <param name="historyExists">Whether Claude's transcript for the session exists.</param>
    /// <param name="hasError">Whether the session has an unresolved error, such as a failed deletion.</param>
    public static SessionState Resolve(Session session, bool isRunning, bool historyExists, bool hasError)
    {
        ArgumentNullException.ThrowIfNull(session);
        return hasError ? SessionState.Error
            : isRunning ? SessionState.Open
            : session.IsArchived ? SessionState.Archived
            : !historyExists && session.LastOpenedAt is not null ? SessionState.HistoryMissing
            : SessionState.Closed;
    }
}

/// <summary>A stored session together with its current state.</summary>
public sealed record SessionSnapshot(Session Session, SessionState State);

/// <summary>What happened when a session was opened.</summary>
public enum OpenOutcome
{
    /// <summary>A new terminal with Claude was started.</summary>
    Launched,

    /// <summary>The session was already running; no second process was started (OTR-08).</summary>
    AlreadyOpen,
}
