using System.Globalization;
using System.Reflection;
using System.IO;
using System.Windows;
using AWL.Core.Setup;
using AWL.Core.Configuration;
using AWL.Infrastructure.Diagnostics;
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

            _host.Start();
            stopping = _host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
            MainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow.Show();

            var pipeline = _host.Services.GetRequiredService<SetupPipeline>();
            while (!stopping.IsCancellationRequested)
            {
                var summary = new List<string>();
                var result = await pipeline.RunAsync(line =>
                {
                    summary.Add(line);
                    LogSetup(logger, line);
                }, stopping);
                if (result.IsSuccess || stopping.IsCancellationRequested)
                {
                    break;
                }

                string message = Strings.ResourceManager.GetString(result.Error.UserMessageKey, CultureInfo.CurrentUICulture)
                    ?? Strings.StartupErrorMessage;
                try
                {
                    string package = SetupDiagnostics.Save(_host.Services.GetRequiredService<AwlPaths>(), version, summary);
                    summary.Add(Strings.SetupPackageSaved + Environment.NewLine + package);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Keep the on-screen summary available even on a read-only disk.
                    LogSetup(logger, $"setup-diagnostics: 0x{ex.HResult:X8}");
                }
                string details = string.Join(Environment.NewLine, summary);
                if (MessageBox.Show(MainWindow, string.Join(Environment.NewLine, message, details, Strings.RetrySetup),
                    Strings.UnexpectedErrorTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    break;
                }
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

    [LoggerMessage(EventId = 1019, Level = LogLevel.Information, Message = "Setup: {Summary}")]
    private static partial void LogSetup(ILogger logger, string summary);

    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "AWL starting. Version: {Version}")]
    private static partial void LogStartup(ILogger logger, string version);
}
