using AWL.Core.Sessions;

namespace AWL.Core.Retention;

/// <summary>Decides which sessions have outlived the retention period (D18, OTR-16, AYR-01).</summary>
public static class RetentionPolicy
{
    public const int MinDays = 1;

    /// <summary>About one hundred years; longer periods add nothing and risk date overflow.</summary>
    public const int MaxDays = 36500;

    public static bool IsValid(int days) => days is >= MinDays and <= MaxDays;

    /// <summary>
    /// A session expires when its last activity (or, if never active, its creation) is older than the period.
    /// The same period is written to Claude's <c>cleanupPeriodDays</c>, so both sides forget at the same time.
    /// </summary>
    public static bool IsExpired(Session session, int retentionDays, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentOutOfRangeException.ThrowIfLessThan(retentionDays, MinDays);
        int days = Math.Min(retentionDays, MaxDays);
        return (session.LastActivityAt ?? session.CreatedAt) < now.AddDays(-days);
    }
}
