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

    public Task<bool> TryBringToFrontAsync(Guid sessionId, CancellationToken token) => Task.Run(() =>
    {
        try
        {
            foreach (var row in Query(token).Where(row => row.Id == sessionId))
            {
                // A console launch owns its own window; Windows Terminal owns the window of its child processes.
                // The walk stops at AWL so that AWL's own window is never mistaken for the session's terminal.
                int? pid = row.Pid;
                for (int depth = 0; pid is int current && current != Environment.ProcessId && depth < 3; depth++)
                {
                    token.ThrowIfCancellationRequested();
                    if (MainWindow(current) is { } window)
                    {
                        if (IsIconic(window))
                        {
                            ShowWindow(window, RestoreWindow);
                        }
                        return SetForegroundWindow(window);
                    }
                    pid = ParentProcessId(current, token);
                }
            }
            return false;
        }
        catch (Exception ex) when (IsQueryFailure(ex))
        {
            LogFailure(logger, ex.HResult);
            return false;
        }
    }, token);

    private static nint? MainWindow(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            nint window = process.MainWindowHandle;
            return window == 0 ? null : window;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private static int? ParentProcessId(int pid, CancellationToken token)
    {
        using var search = new ManagementObjectSearcher(FormattableString.Invariant(
            $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {pid}"));
        using var rows = search.Get();
        foreach (ManagementObject row in rows)
        {
            using (row)
            {
                token.ThrowIfCancellationRequested();
                return row["ParentProcessId"] is uint parent && parent != 0 ? checked((int)parent) : null;
            }
        }
        return null;
    }

    private const int RestoreWindow = 9;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(nint window, int command);

    private static List<(Guid Id, int Pid)> Query(CancellationToken token, int? pid = null)
    {
        using var identity = WindowsIdentity.GetCurrent();
        string? sid = identity.User?.Value;
        var result = new List<(Guid, int)>();
        if (sid is null)
        {
            return result;
        }
        string query = "SELECT Handle, ProcessId, CommandLine FROM Win32_Process WHERE Name = 'claude.exe'";
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
                using var owner = row.InvokeMethod("GetOwnerSid", null, null);
                if (owner?["ReturnValue"] is uint status && status == 0 &&
                    string.Equals(owner["Sid"] as string, sid, StringComparison.Ordinal))
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
