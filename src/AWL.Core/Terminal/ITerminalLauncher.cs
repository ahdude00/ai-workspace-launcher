using AWL.Core.Common;

namespace AWL.Core.Terminal;

public interface ITerminalLauncher
{
    bool IsAvailable { get; }
    Task<Result> LaunchAsync(ClaudeLaunchSpec spec, string windowTitle, CancellationToken cancellationToken);
}
