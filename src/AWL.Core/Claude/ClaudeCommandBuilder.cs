using AWL.Core.Configuration;
using AWL.Core.Sessions;
using AWL.Core.Terminal;

namespace AWL.Core.Claude;

public sealed class ClaudeCommandBuilder(AwlPaths paths, IClaudeInstallation? installation = null)
{
    /// <summary>Builds the invocation for a stored session using the supplied or default Claude executable.</summary>
    public ClaudeLaunchSpec Build(Session session, LaunchMode mode, string? executablePath = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        string executable = executablePath ?? installation?.ExecutablePath ?? paths.DefaultClaudeExecutable;
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        string sessionFlag = mode switch
        {
            LaunchMode.New => ClaudeCli.SessionId,
            LaunchMode.Resume => ClaudeCli.Resume,
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

        return new ClaudeLaunchSpec(executable,
            [sessionFlag, session.ClaudeSessionId.ToString("D"),
             ClaudeCli.Settings, paths.ClaudeSettingsFile,
             ClaudeCli.AppendSystemPromptFile, paths.ContextFile],
            session.WorkspacePath);
    }
}
