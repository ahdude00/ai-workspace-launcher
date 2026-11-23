using System.Globalization;
using System.Reflection;
using System.Windows;
using AWL.Core.Configuration;
using AWL.Core.Claude;
using AWL.Core.Python;
using AWL.Desktop.Resources;
using AWL.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AWL.Desktop;

/// <summary>
/// Composition root: builds the host, shows the main window, disposes the host on exit.
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private GlobalExceptionHandler? _exceptionHandler;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        CancellationToken stopping = default;

        try
        {
            _host = AppHost.Build(e.Args);
            ILogger<App> logger = _host.Services.GetRequiredService<ILogger<App>>();
            _exceptionHandler = new GlobalExceptionHandler(
                Dispatcher, logger, () => ShowError(Strings.UnexpectedErrorMessage));

            Assembly assembly = typeof(App).Assembly;
            string version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "unknown";
            LogStartup(logger, version);

            _host.Services.GetRequiredService<IAwlSettingsStore>().RefreshClaudeFiles();
            _host.Start();
            stopping = _host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
            MainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow.Show();

            var installation = _host.Services.GetRequiredService<IClaudeInstallation>();
            var result = await installation.EnsureInstalledAsync(stopping);
            if (!result.IsSuccess && !stopping.IsCancellationRequested)
            {
                ShowError(Strings.ResourceManager.GetString(result.Error.UserMessageKey, CultureInfo.CurrentUICulture)
                    ?? Strings.ClaudeInstallFailed);
                return;
            }

            stopping.ThrowIfCancellationRequested();
            var python = _host.Services.GetRequiredService<IPythonEnvironment>();
            var setup = await python.EnsureReadyAsync(stopping);
            if (!setup.IsSuccess && !stopping.IsCancellationRequested)
            {
                ShowError(Strings.PythonSetupFailed);
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // The user closed AWL during setup.
        }
        catch (Exception exception)
        {
            _exceptionHandler?.LogStartupFailure(exception);
            ShowError(Strings.StartupErrorMessage);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Services.GetService<IHostApplicationLifetime>()?.StopApplication();
        _exceptionHandler?.Detach();
        _host?.Dispose();
        base.OnExit(e);
    }

    private void ShowError(string message)
    {
        if (MainWindow is { IsVisible: true } owner)
        {
            MessageBox.Show(owner, message, Strings.UnexpectedErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        MessageBox.Show(message, Strings.UnexpectedErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "AWL starting. Version: {Version}")]
    private static partial void LogStartup(ILogger logger, string version);
}
