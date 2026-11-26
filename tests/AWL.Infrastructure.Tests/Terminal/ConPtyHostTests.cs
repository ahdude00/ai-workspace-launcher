using System.Text;
using AWL.Core.Terminal;
using AWL.Infrastructure.Terminal;

namespace AWL.Infrastructure.Tests.Terminal;

public sealed class ConPtyHostTests
{
    [Fact]
    public async Task Console_child_reads_input_and_returns_its_environment()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return;
        }
        string marker = Guid.NewGuid().ToString("N");
        var spec = new ClaudeLaunchSpec(Path.Combine(AppContext.BaseDirectory, "ClaudeFixture", "claude.exe"),
            ["--pty-test", "two words"], Path.GetTempPath());
        using var host = ConPtyHost.Start(spec, 100, 30,
            new Dictionary<string, string> { ["AWL_PTY_TEST"] = marker });
        host.Resize(110, 32);
        using var text = new StreamReader(host.Output, Encoding.UTF8, leaveOpen: true);
        string expected = $"two words:ping:{marker}";
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = Task.Run(async () =>
        {
            var buffer = new char[512];
            var transcript = new StringBuilder();
            while (!transcript.ToString().Contains(expected, StringComparison.Ordinal))
            {
                int count = await text.ReadAsync(buffer);
                if (count == 0)
                {
                    break;
                }
                transcript.Append(buffer, 0, count);
                if (transcript.ToString().Contains("ready", StringComparison.Ordinal))
                {
                    ready.TrySetResult();
                }
            }
            return transcript.ToString();
        });
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await host.Input.WriteAsync(Encoding.UTF8.GetBytes("ping\r"));
        await host.Input.FlushAsync();
        (await response.WaitAsync(TimeSpan.FromSeconds(15))).ShouldContain(expected);
    }
}
