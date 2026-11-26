using AWL.Core.Common;

namespace AWL.Core.Python;

public interface IPythonEnvironment
{
    /// <summary>The Python version of AWL's virtual environment, or null when it has not been created.</summary>
    string? InstalledVersion { get; }

    Task<bool> CheckAsync(CancellationToken cancellationToken);
    Task<Result> EnsureReadyAsync(CancellationToken cancellationToken);
}
