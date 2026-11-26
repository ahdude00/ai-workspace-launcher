using System.ComponentModel;
using System.Diagnostics;
using AWL.Core.Common;
using AWL.Core.Configuration;
using AWL.Core.Terminal;

namespace AWL.Infrastructure.Terminal;

internal abstract class ProcessTerminalLauncher : ITerminalLauncher
{
    public abstract bool IsAvailable { get; }
    internal abstract ProcessStartInfo Build(ClaudeLaunchSpec spec, string windowTitle);

    public Task<Result> LaunchAsync(ClaudeLaunchSpec spec, string windowTitle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var process = Process.Start(Build(spec, windowTitle));
            return Task.FromResult(process is null ? Failure() : Result.Ok());
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(Failure());
        }
    }

    private static Result Failure() => Result.Fail(new AppError("TerminalLaunchFailed", "TerminalLaunchFailed"));
}

internal sealed class WindowsTerminalLauncher(PortableTerminalStep terminal) : ProcessTerminalLauncher
{
    public override bool IsAvailable => terminal.IsInstalled && PortableTerminalStep.IsSupported;

    internal override ProcessStartInfo Build(ClaudeLaunchSpec spec, string windowTitle)
    {
        var start = new ProcessStartInfo(terminal.Executable)
        {
            UseShellExecute = false,
            WorkingDirectory = spec.WorkingDirectory,
            ArgumentList = { "-w", "new", "new-tab", "--title", Escape(windowTitle), "-d",
                Escape(spec.WorkingDirectory), "--", Escape(spec.Executable) },
        };
        foreach (string argument in spec.Arguments)
        {
            start.ArgumentList.Add(Escape(argument));
        }
        return start;
    }

    private static string Escape(string value) => value.Replace(";", "\\;", StringComparison.Ordinal);
}

internal sealed class ConsoleLauncher : ProcessTerminalLauncher
{
    public override bool IsAvailable => OperatingSystem.IsWindows();

    internal override ProcessStartInfo Build(ClaudeLaunchSpec spec, string windowTitle)
    {
        var start = new ProcessStartInfo(spec.Executable)
        {
            UseShellExecute = false,
            CreateNoWindow = false,
            WorkingDirectory = spec.WorkingDirectory,
        };
        foreach (string argument in spec.Arguments)
        {
            start.ArgumentList.Add(argument);
        }
        return start;
    }
}

internal sealed class TerminalLauncherSelector(IAwlSettingsStore settings, ITerminalLauncher terminal, ITerminalLauncher console)
    : IExternalTerminalLauncher
{
    public bool IsAvailable => terminal.IsAvailable || console.IsAvailable;

    public Task<Result> LaunchAsync(ClaudeLaunchSpec spec, string windowTitle, CancellationToken cancellationToken)
    {
        ITerminalLauncher launcher = settings.Load().TerminalPreference != TerminalPreference.WindowsConsole && terminal.IsAvailable
            ? terminal : console;
        return launcher.LaunchAsync(spec, windowTitle, cancellationToken);
    }
}
