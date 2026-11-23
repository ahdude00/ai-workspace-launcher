using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using AWL.Core.Claude;
using AWL.Core.Common;
using AWL.Core.Configuration;
using AWL.Infrastructure.Processes;
using Microsoft.Extensions.Logging;

namespace AWL.Infrastructure.Claude;

internal sealed partial class PowerShellClaudeInstaller(
    AwlPaths paths,
    ILogger<PowerShellClaudeInstaller> logger,
    Func<ProcessStartInfo, CancellationToken, Task<CommandOutput>> run,
    Func<string?> searchPath) : IClaudeInstallation
{
    private const int RestrictedLanguage = 85;
    private const string InstallCommand = """
        $ErrorActionPreference = 'Stop'
        if ($ExecutionContext.SessionState.LanguageMode -ne 'FullLanguage') { exit 85 }
        try { Invoke-RestMethod 'https://claude.ai/install.ps1' | Invoke-Expression }
        catch { Write-Output ('AWL_INSTALL_ERROR=' + $_.CategoryInfo.Category); exit 1 }
        """;

    public string? ExecutablePath { get; private set; }

    public async Task<Result<string>> EnsureInstalledAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecutablePath = null;
            string? executable = FindExecutable();
            if (executable is null)
            {
                string powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
                CommandOutput install = await run(StartInfo(powershell, "-NoProfile", "-NonInteractive",
                    "-Command", InstallCommand), timeout.Token).ConfigureAwait(false);
                // Raw installer output can contain user paths and proxy credentials.
                LogInstallerOutput(logger, install.ExitCode, InstallerCategory().Match(install.StandardOutput).Groups[1].Value,
                    install.StandardOutput.Length, install.StandardError.Length);
                if (install.ExitCode == RestrictedLanguage)
                {
                    return Failure("ClaudePowerShellRestricted");
                }

                executable = FindExecutable();
                if (install.ExitCode != 0 || executable is null)
                {
                    return Failure("ClaudeInstallFailed");
                }
            }

            CommandOutput probe = await run(StartInfo(executable, ClaudeCli.Version), timeout.Token).ConfigureAwait(false);
            Match version = VersionOutput().Match(probe.StandardOutput.Trim());
            if (probe.ExitCode != 0 || !version.Success)
            {
                LogProbeFailure(logger, probe.ExitCode);
                return Failure("ClaudeVersionFailed");
            }

            ExecutablePath = executable;
            string value = version.Groups[1].Value;
            LogVersion(logger, value);
            return Result.Ok(value);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("ClaudeSetupTimeout");
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException)
        {
            LogProcessFailure(logger, ex.HResult);
            return Failure("ClaudeSetupBlocked");
        }
    }

    private string? FindExecutable()
    {
        if (File.Exists(paths.DefaultClaudeExecutable))
        {
            return paths.DefaultClaudeExecutable;
        }

        foreach (string entry in (searchPath() ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string directory = entry.Trim().Trim('"');
            if (Path.IsPathFullyQualified(directory))
            {
                string candidate = Path.Combine(directory, "claude.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static ProcessStartInfo StartInfo(string executable, params string[] arguments)
    {
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        return info;
    }

    private static Result<string> Failure(string code) => Result.Fail<string>(new AppError(code, code));

    [GeneratedRegex(@"\A(\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.+-]+)?)(?:\s+\(Claude Code\))?\z", RegexOptions.CultureInvariant)]
    private static partial Regex VersionOutput();

    [GeneratedRegex(@"(?m)^AWL_INSTALL_ERROR=([A-Za-z]{1,40})\r?$", RegexOptions.CultureInvariant)]
    private static partial Regex InstallerCategory();

    [LoggerMessage(1600, LogLevel.Information, "Claude Code version: {Version}")]
    private static partial void LogVersion(ILogger logger, string version);

    [LoggerMessage(1601, LogLevel.Information, "Claude installer exit {ExitCode}, PowerShell category {Category}, stdout {OutputLength} chars, stderr {ErrorLength} chars.")]
    private static partial void LogInstallerOutput(ILogger logger, int exitCode, string category, int outputLength, int errorLength);

    [LoggerMessage(1602, LogLevel.Warning, "Claude version verification failed (exit {ExitCode}).")]
    private static partial void LogProbeFailure(ILogger logger, int exitCode);

    [LoggerMessage(1603, LogLevel.Warning, "Claude setup process failed (HRESULT {HResult}).")]
    private static partial void LogProcessFailure(ILogger logger, int hResult);
}
