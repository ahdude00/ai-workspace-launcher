using System.Security;
using AWL.Core.Common;
using AWL.Core.Setup;
using AWL.Core.Shell;
using AWL.Desktop.Resources;

namespace AWL.Desktop;

/// <summary>Setup step "Windows entegrasyonu": the "AWL ile aç" context menu (KUR-03, SAG-01…03).</summary>
internal sealed class ShellIntegrationStep(IShellIntegration shell, Func<string?>? executable = null) : ISetupStep
{
    public string Id => "shell";

    // After an update or a move, the command must point at the AWL.exe that is running now.
    private string? Executable => (executable ?? (() => Environment.ProcessPath))();

    public Task<bool> CheckAsync(CancellationToken token) =>
        Task.FromResult(Executable is { } path && shell.IsRegistered(path, Strings.ShellMenuText));

    public Task<Result> RunAsync(CancellationToken token)
    {
        if (Executable is not { } path)
        {
            return Task.FromResult(Failure("NoProcessPath"));
        }
        try
        {
            shell.Register(path, Strings.ShellMenuText);
            return Task.FromResult(Result.Ok());
        }
        catch (SecurityException)
        {
            // A policy can forbid writing below HKCU\Software\Classes; AWL explains instead of working around it.
            return Task.FromResult(Failure("SecurityException"));
        }
    }

    private static Result Failure(string detail) =>
        Result.Fail(new AppError("ShellIntegrationFailed", "ShellIntegrationFailed", detail));
}
