using AWL.Core.Claude;
using AWL.Core.Common;
using AWL.Core.Configuration;
using AWL.Core.Setup;
using AWL.Core.Terminal;
using AWL.Desktop.Resources;
using AWL.Desktop.Views;

namespace AWL.Desktop;

internal sealed class ClaudeLoginStep(IAwlSettingsStore settings, IClaudeInstallation installation,
    ITerminalLauncher launcher, AwlPaths paths, MainWindow window) : ISetupStep
{
    public string Id => "login";
    public Task<bool> CheckAsync(CancellationToken token) =>
        Task.FromResult(settings.Load().SetupSteps.GetValueOrDefault(Id) == SetupStepStatus.Completed);

    public async Task<Result> RunAsync(CancellationToken token)
    {
        var spec = new ClaudeLaunchSpec(installation.ExecutablePath ?? paths.DefaultClaudeExecutable,
            [ClaudeCli.Settings, paths.ClaudeSettingsFile], paths.AwlHome);
        while (true)
        {
            var launched = await launcher.LaunchAsync(spec, Strings.LoginTitle, token).ConfigureAwait(false);
            if (!launched.IsSuccess)
            {
                return launched;
            }
            var choice = await window.Dispatcher.InvokeAsync(() =>
            {
                window.Workspace.Visibility = System.Windows.Visibility.Collapsed;
                window.Wizard.Visibility = System.Windows.Visibility.Visible;
                return window.Wizard.WaitForLoginAsync(token);
            });
            if (await choice.ConfigureAwait(false))
            {
                return Result.Ok();
            }
        }
    }
}
