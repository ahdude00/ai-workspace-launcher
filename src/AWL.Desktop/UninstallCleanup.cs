using System.IO;
using AWL.Core.Claude;
using AWL.Core.Configuration;
using AWL.Core.Sessions;
using AWL.Core.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace AWL.Desktop;

internal static class UninstallCleanup
{
    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Skip(1).Any(arg => arg is not ("--remove-awl-history" or "--remove-claude")))
        {
            return 1;
        }

        try
        {
            using var host = AppHost.Build([]);
            var paths = host.Services.GetRequiredService<AwlPaths>();
            host.Services.GetRequiredService<IShellIntegration>().Unregister();
            if (args.Contains("--remove-awl-history", StringComparer.Ordinal) && File.Exists(paths.DatabaseFile))
            {
                var sessions = await host.Services.GetRequiredService<ISessionRepository>().ListAsync();
                var history = host.Services.GetRequiredService<IClaudeHistoryStore>();
                foreach (var session in sessions)
                {
                    if (!(await history.DeleteAsync(session.ClaudeSessionId, CancellationToken.None)).IsComplete)
                    {
                        return 1;
                    }
                }
            }
            if (args.Contains("--remove-claude", StringComparer.Ordinal) && File.Exists(paths.DefaultClaudeExecutable))
            {
                File.Delete(paths.DefaultClaudeExecutable);
            }
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Data.Common.DbException
            or System.Security.SecurityException)
        {
            return 1;
        }
    }
}
