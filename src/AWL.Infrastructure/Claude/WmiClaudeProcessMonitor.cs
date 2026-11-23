using System.Collections.Frozen;
using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using AWL.Core.Claude;
using AWL.Core.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AWL.Infrastructure.Claude;

internal sealed partial class WmiClaudeProcessMonitor(ILogger<WmiClaudeProcessMonitor> logger)
    : BackgroundService, IClaudeProcessMonitor
{
    private volatile FrozenSet<Guid> _running = FrozenSet<Guid>.Empty;
    public IReadOnlySet<Guid> RunningSessionIds => _running;
    public event EventHandler? Changed;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        do
        {
            try
            {
                var current = Query(stoppingToken).Select(row => row.Id).ToFrozenSet();
                if (!_running.SetEquals(current))
                {
                    _running = current;
                    Changed?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex) when (IsQueryFailure(ex))
            {
                LogFailure(logger, ex.HResult);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    public Task<Result> StopSessionAsync(Guid sessionId, CancellationToken token) => Task.Run(() =>
    {
        try
        {
            foreach (var row in Query(token).Where(row => row.Id == sessionId))
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using var process = Process.GetProcessById(row.Pid);
                    // Retain the process handle and verify its identity again before stopping its tree.
                    _ = process.SafeHandle;
                    if (!process.HasExited && Query(token, row.Pid).Any(item => item.Id == sessionId))
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (ArgumentException)
                {
                    // The PID disappeared before we acquired the handle.
                }
                catch (InvalidOperationException)
                {
                    // The process exited during inspection.
                }
            }
            return Result.Ok();
        }
        catch (Exception ex) when (IsQueryFailure(ex))
        {
            LogFailure(logger, ex.HResult);
            return Result.Fail(new AppError("ClaudeStopFailed", "ClaudeStopFailed"));
        }
    }, token);

    private static List<(Guid Id, int Pid)> Query(CancellationToken token, int? pid = null)
    {
        using var identity = WindowsIdentity.GetCurrent();
        string? sid = identity.User?.Value;
        var result = new List<(Guid, int)>();
        if (sid is null)
        {
            return result;
        }
        string query = "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'claude.exe'";
        if (pid.HasValue)
        {
            query += FormattableString.Invariant($" AND ProcessId = {pid.Value}");
        }
        using var search = new ManagementObjectSearcher(query);
        using var rows = search.Get();
        foreach (ManagementObject row in rows)
        {
            using (row)
            {
                token.ThrowIfCancellationRequested();
                if (ClaudeProcessArguments.SessionId(row["CommandLine"] as string) is not Guid id)
                {
                    continue;
                }
                object[] owner = [string.Empty];
                if (row.InvokeMethod("GetOwnerSid", owner) is uint status && status == 0 &&
                    string.Equals(owner[0] as string, sid, StringComparison.Ordinal))
                {
                    result.Add((id, checked((int)(uint)row["ProcessId"])));
                }
            }
        }
        return result;
    }

    private static bool IsQueryFailure(Exception ex) =>
        ex is ManagementException or COMException or UnauthorizedAccessException or Win32Exception;

    [LoggerMessage(1610, LogLevel.Warning, "Claude process query or stop failed (HRESULT {HResult:X8}).")]
    private static partial void LogFailure(ILogger logger, int hResult);
}
