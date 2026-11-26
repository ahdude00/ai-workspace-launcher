namespace AWL.Core.Claude;

public sealed record HistoryInfo(DateTimeOffset LastModifiedUtc);

public sealed record DeleteFailure(string Path, string Reason);

public sealed record DeleteReport(IReadOnlyList<DeleteFailure> Failures)
{
    public bool IsComplete => Failures.Count == 0;
}

public interface IClaudeHistoryStore
{
    HistoryInfo? Find(Guid claudeSessionId);
    Task<DeleteReport> DeleteAsync(Guid claudeSessionId, CancellationToken cancellationToken);
}
