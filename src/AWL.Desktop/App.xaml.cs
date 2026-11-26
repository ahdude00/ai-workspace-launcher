using System.Globalization;
using System.IO;
using System.Windows;
using AWL.Core.Setup;
using AWL.Core.Configuration;
using AWL.Core.Shell;
using AWL.Infrastructure.Ipc;
using AWL.Infrastructure.Diagnostics;
using AWL.Desktop.Messages;
using AWL.Desktop.Resources;
using AWL.Desktop.Services;
using AWL.Desktop.ViewModels;
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
    private static readonly TimeSpan ForwardTimeout = TimeSpan.FromSeconds(5);

    private IHost? _host;
    private GlobalExceptionHandler? _exceptionHandler;
    private SingleInstanceLock? _instanceLock;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        CancellationToken stopping = default;

        // SAG-04: only known flags with absolute, existing, non-device paths. Anything else just activates AWL.
        var parsed = LaunchArgumentParser.Parse(e.Args);
        LaunchRequest request = parsed.IsSuccess ? parsed.Value : LaunchRequest.Activate;

        // SAG-05: a second AWL hands its request to the running one and exits without opening a window.
        _instanceLock = SingleInstanceLock.TryAcquire();
        if (_instanceLock is null)
        {
            if (!parsed.IsSuccess)
            {
                ShowError(Message(parsed.Error.UserMessageKey));
            }
            ForegroundHandoff.AllowAny();
            if (!await NamedPipeLaunchRequestSource.TrySendAsync(request, ForwardTimeout))
            {
                ShowError(Strings.AlreadyRunningNoAnswer);
            }
            Shutdown(0);
            return;
        }

        try
        {
            _host = AppHost.Build([]);
            ILogger<App> logger = _host.Services.GetRequiredService<ILogger<App>>();
            _exceptionHandler = new GlobalExceptionHandler(
                Dispatcher, logger, () => ShowError(Strings.UnexpectedErrorMessage));

            string version = DesktopModule.ReadVersion();
            LogStartup(logger, version);

            // Subscribe before the pipe listener starts so that no forwarded request is lost.
            var launches = _host.Services.GetRequiredService<LaunchRequestCoordinator>();
            _host.Start();
            stopping = _host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
            StringResources.AddTo(Resources);
            var window = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = window;
            MainWindow.Show();
            if (!parsed.IsSuccess)
            {
                ShowError(Message(parsed.Error.UserMessageKey));
            }
            await launches.SubmitAsync(request);

            _host.Services.GetRequiredService<SessionChangeRelay>();
            var pipeline = _host.Services.GetRequiredService<SetupPipeline>();
            var wizard = window.Wizard;
            var settings = _host.Services.GetRequiredService<IAwlSettingsStore>();
            bool firstRun = settings.Load().SetupSteps.GetValueOrDefault("login") != SetupStepStatus.Completed;
            while (!stopping.IsCancellationRequested)
            {
                wizard.Reset(pipeline.StepIds);
                if (firstRun)
                {
                    wizard.Visibility = Visibility.Visible;
                    window.Workspace.Visibility = Visibility.Collapsed;
                }
                var summary = new List<string>();
                var result = await pipeline.RunAsync(line =>
                {
                    summary.Add(line);
                    LogSetup(logger, line);
                }, (id, status) => Dispatcher.Invoke(() => wizard.Update(id, status)), stopping);
                if (result.IsSuccess || stopping.IsCancellationRequested)
                {
                    wizard.Visibility = Visibility.Collapsed;
                    window.Workspace.Visibility = Visibility.Visible;
                    await _host.Services.GetRequiredService<MainWindowViewModel>().InitializeAsync(stopping);
                    await launches.MarkReadyAsync();
                    break;
                }

                firstRun = true;
                wizard.Visibility = Visibility.Visible;
                window.Workspace.Visibility = Visibility.Collapsed;
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
                    LogDiagnosticsWriteFailure(logger, ex.HResult);
                }
                string details = string.Join(Environment.NewLine, summary);
                await wizard.WaitForRetryAsync(message, details, stopping);
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
        _instanceLock?.Dispose();
        base.OnExit(e);
    }

    private static string Message(string key) =>
        Strings.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? Strings.StartupErrorMessage;

    private void ShowError(string message)
    {
        if (MainWindow is { IsVisible: true } owner)
        {
            MessageBox.Show(owner, message, Strings.UnexpectedErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        MessageBox.Show(message, Strings.UnexpectedErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    [LoggerMessage(EventId = 1020, Level = LogLevel.Warning, Message = "Setup diagnostics could not be saved (HRESULT {HResult:X8}).")]
    private static partial void LogDiagnosticsWriteFailure(ILogger logger, int hResult);

    [LoggerMessage(EventId = 1019, Level = LogLevel.Information, Message = "Setup: {Summary}")]
    private static partial void LogSetup(ILogger logger, string summary);

    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "AWL starting. Version: {Version}")]
    private static partial void LogStartup(ILogger logger, string version);
}
