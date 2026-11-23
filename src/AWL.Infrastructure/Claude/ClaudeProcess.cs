using System.Diagnostics;

namespace AWL.Infrastructure.Claude;

internal sealed record CommandOutput(int ExitCode, string StandardOutput, string StandardError);

internal static class ClaudeProcess
{
    internal static async Task<CommandOutput> RunAsync(ProcessStartInfo info, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var process = Process.Start(info) ?? throw new IOException("Process did not start.");
        try
        {
            Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            await Task.WhenAll(stdout, stderr, process.WaitForExitAsync(cancellationToken)).ConfigureAwait(false);
            return new CommandOutput(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                    // Process exited between the status check and termination.
                }
            }

            throw;
        }
    }
}
