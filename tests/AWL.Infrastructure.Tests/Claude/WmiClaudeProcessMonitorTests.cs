using System.Diagnostics;
using AWL.Infrastructure.Claude;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace AWL.Infrastructure.Tests.Claude;

[CollectionDefinition("Process monitoring", DisableParallelization = true)]
public sealed class ProcessMonitoringTestsGroup;

[Collection("Process monitoring")]
public sealed class WmiClaudeProcessMonitorTests(ITestOutputHelper output)
{
    [Fact]
    public void Reads_only_whole_option_tokens_before_the_argument_delimiter()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var id = Guid.NewGuid();
        ClaudeProcessArguments.SessionId($"claude.exe --resume {id:D}").ShouldBe(id);
        ClaudeProcessArguments.SessionId($"claude.exe --session-id \"{id:D}\"").ShouldBe(id);
        ClaudeProcessArguments.SessionId($"claude.exe \"metin --resume {id:D}\"").ShouldBeNull();
        ClaudeProcessArguments.SessionId($"claude.exe -- --session-id {id:D}").ShouldBeNull();
    }

    [Fact]
    public async Task Discovers_preexisting_session_and_tracks_its_exit()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var id = Guid.NewGuid();
        var folder = Directory.CreateTempSubdirectory();
        using var monitor = new WmiClaudeProcessMonitor(NullLogger<WmiClaudeProcessMonitor>.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var process = new Process();
        try
        {
            string executable = Path.Combine(folder.FullName, "claude.exe");
            File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), executable);
            process.StartInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                ArgumentList = { "/d", "/q", "/k", "rem", "--session-id", id.ToString("D") },
            };
            process.Start().ShouldBeTrue();
            var detected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var removed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            monitor.Changed += (_, _) =>
            {
                if (monitor.RunningSessionIds.Contains(id))
                {
                    detected.TrySetResult();
                }
                else if (detected.Task.IsCompleted)
                {
                    removed.TrySetResult();
                }
            };
            await monitor.StartAsync(timeout.Token);
            await detected.Task.WaitAsync(TimeSpan.FromSeconds(3), timeout.Token);
            (await monitor.StopSessionAsync(id, timeout.Token)).IsSuccess.ShouldBeTrue();
            await process.WaitForExitAsync(timeout.Token);
            await removed.Task.WaitAsync(TimeSpan.FromSeconds(3), timeout.Token);

            using var testHost = Process.GetCurrentProcess();
            var cpu = testHost.TotalProcessorTime;
            var elapsed = Stopwatch.StartNew();
            await Task.Delay(TimeSpan.FromSeconds(8), timeout.Token);
            testHost.Refresh();
            double utilization = (testHost.TotalProcessorTime - cpu) / elapsed.Elapsed * 100;
            output.WriteLine($"Idle monitor and test host CPU (one core): {utilization:F2}%");
            utilization.ShouldBeLessThan(1.0);
        }
        finally
        {
            await monitor.StopAsync(CancellationToken.None);
            if (process.StartInfo.FileName.Length > 0 && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
            folder.Delete(recursive: true);
        }
    }
}
