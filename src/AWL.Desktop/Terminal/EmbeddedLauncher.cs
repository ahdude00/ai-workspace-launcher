using System.Windows;
using AWL.Core.Claude;
using AWL.Core.Common;
using AWL.Core.Configuration;
using AWL.Core.Terminal;
using AWL.Desktop.Views;

namespace AWL.Desktop.Terminal;

internal sealed class EmbeddedLauncher(IAwlSettingsStore settings, IExternalTerminalLauncher external) : ITerminalLauncher
{
    public bool IsAvailable => external.IsAvailable || OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763);

    public async Task<Result> LaunchAsync(ClaudeLaunchSpec spec, string windowTitle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (settings.Load().TerminalPreference == TerminalPreference.Embedded &&
            (spec.Arguments.Contains(ClaudeCli.SessionId) || spec.Arguments.Contains(ClaudeCli.Resume)))
        {
            var opened = await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763) && Application.Current.MainWindow is MainWindow window)
                {
                    return await TerminalBridge.TryOpenAsync(window, spec, windowTitle, cancellationToken);
                }
                return null;
            });
            if (await opened.ConfigureAwait(false) is Result result)
            {
                return result;
            }
        }
        return await external.LaunchAsync(spec, windowTitle, cancellationToken).ConfigureAwait(false);
    }
}
