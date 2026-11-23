using System.ComponentModel;
using System.Diagnostics;
using AWL.Core.Common;
using AWL.Core.Configuration;
using AWL.Core.Python;
using AWL.Infrastructure.Processes;
using Microsoft.Extensions.Logging;

namespace AWL.Infrastructure.Python;

internal sealed partial class UvPythonEnvironment(
    AwlPaths paths, ILogger<UvPythonEnvironment> logger,
    Func<ProcessStartInfo, CancellationToken, Task<CommandOutput>> run) : IPythonEnvironment
{
    private static readonly string AssetDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Python");
    private static readonly string LockFile = Path.Combine(AssetDirectory, "requirements.lock");
    private static readonly string Uv = Path.Combine(AppContext.BaseDirectory, "third_party", "uv.exe");

    public async Task<bool> CheckAsync(CancellationToken cancellationToken) =>
        await ProbeAsync(cancellationToken).ConfigureAwait(false) == 0;

    public async Task<Result> EnsureReadyAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(20));
        CancellationToken token = timeout.Token;
        try
        {
            Directory.CreateDirectory(paths.AwlHome);
            using var lease = await AcquireSetupLockAsync(token).ConfigureAwait(false);
            int state = await ProbeAsync(token).ConfigureAwait(false);
            if (state == 0)
            {
                return Result.Ok();
            }

            if (state != 1)
            {
                if (!await RunUvAsync(["python", "install", "3.13", "--no-bin", "--no-registry"], token).ConfigureAwait(false)
                    || !await RunUvAsync(["venv", "--managed-python", "--no-project", "--python", "3.13", "--seed",
                        "--clear", paths.VirtualEnvironmentDirectory], token).ConfigureAwait(false))
                {
                    return Failed();
                }
            }

            bool installed = await RunUvAsync(["pip", "install", "--python", paths.PythonExecutable, "--require-hashes",
                "--only-binary", ":all:", "--reinstall", "-r", LockFile], token).ConfigureAwait(false);
            return installed && await CheckAsync(token).ConfigureAwait(false) ? Result.Ok() : Failed();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogFailure(logger, "timeout", 0);
            return Failed();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception)
        {
            LogFailure(logger, "setup", ex.HResult);
            return Failed();
        }
    }

    private async Task<FileStream> AcquireSetupLockAsync(CancellationToken token)
    {
        string path = Path.Combine(paths.AwlHome, "python-setup.lock");
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException ex) when ((ex.HResult & 0xffff) is 32 or 33)
            {
                await Task.Delay(250, token).ConfigureAwait(false);
            }
        }
    }

    private async Task<int> ProbeAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!File.Exists(paths.PythonExecutable))
        {
            return 2;
        }

        var info = StartInfo(paths.PythonExecutable, ["-I", Path.Combine(AssetDirectory, "check_environment.py"),
            paths.VirtualEnvironmentDirectory, paths.PythonInstallDirectory, LockFile]);
        return (await run(info, token).ConfigureAwait(false)).ExitCode;
    }

    private async Task<bool> RunUvAsync(string[] arguments, CancellationToken token)
    {
        var info = StartInfo(Uv, arguments);
        foreach (string key in info.Environment.Keys.Where(key =>
            key.StartsWith("UV_", StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("PIP_", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            info.Environment.Remove(key);
        }

        info.Environment.Remove("VIRTUAL_ENV");
        info.Environment["UV_PYTHON_INSTALL_DIR"] = paths.PythonInstallDirectory;
        info.Environment["UV_CACHE_DIR"] = paths.UvCacheDirectory;
        info.Environment["UV_NATIVE_TLS"] = "1";
        info.ArgumentList.Add("--no-config");
        CommandOutput output = await run(info, token).ConfigureAwait(false);
        if (output.ExitCode != 0)
        {
            LogFailure(logger, arguments[0], output.ExitCode);
        }

        return output.ExitCode == 0;
    }

    private ProcessStartInfo StartInfo(string executable, string[] arguments)
    {
        var info = new ProcessStartInfo(executable)
        {
            WorkingDirectory = paths.AwlHome,
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

    private static Result Failed() => Result.Fail(new AppError("PythonSetupFailed", "PythonSetupFailed"));

    [LoggerMessage(1700, LogLevel.Warning, "AWL Python environment step {Step} failed ({Code}).")]
    private static partial void LogFailure(ILogger logger, string step, int code);
}
