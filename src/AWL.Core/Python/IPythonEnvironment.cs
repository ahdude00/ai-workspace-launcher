using AWL.Core.Common;

namespace AWL.Core.Python;

public interface IPythonEnvironment
{
    Task<bool> CheckAsync(CancellationToken cancellationToken);
    Task<Result> EnsureReadyAsync(CancellationToken cancellationToken);
}
