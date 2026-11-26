using System.Globalization;
using System.IO;
using System.Windows;
using AWL.Core.Configuration;
using AWL.Core.Setup;
using AWL.Desktop.Resources;
using AWL.Desktop.Services;
using AWL.Desktop.ViewModels;
using AWL.Desktop.Views;
using AWL.Infrastructure.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AWL.Desktop;

/// <summary>
/// Runs the setup pipeline with the wizard: at startup and for "Onar" (KUR-14, KUR-17). Each step checks itself
/// first, so a repair only redoes what is missing; a failed step offers "Tekrar dene" until it succeeds.
/// </summary>
internal sealed partial class SetupRunner(MainWindow window, SetupPipeline pipeline, AwlPaths paths, AppVersion version,
    IHostApplicationLifetime lifetime, IUiDispatcher dispatcher, MainWindowViewModel main, ILogger<SetupRunner> logger)
    : IRepairService
{
    private bool _running;

    /// <summary>Runs until setup succeeds or AWL closes. Must be called on the UI thread.</summary>
    public async Task RunAsync(bool showWizard, CancellationToken stopping)
    {
        if (_running)
        {
            return;
        }
        _running = true;
        var wizard = window.Wizard;
        try
        {
            while (!stopping.IsCancellationRequested)
            {
                wizard.Reset(pipeline.StepIds);
                if (showWizard)
                {
                    ShowWizard(true);
                }
                var summary = new List<string>();
                var result = await pipeline.RunAsync(line =>
                {
                    summary.Add(line);
                    LogSetup(logger, line);
                }, (id, status) => window.Dispatcher.Invoke(() => wizard.Update(id, status)), stopping);
                if (result.IsSuccess || stopping.IsCancellationRequested)
                {
                    ShowWizard(false);
                    return;
                }

                showWizard = true;
                ShowWizard(true);
                string message = Strings.ResourceManager.GetString(result.Error.UserMessageKey, CultureInfo.CurrentUICulture)
                    ?? Strings.StartupErrorMessage;
                try
                {
                    string package = SetupDiagnostics.Save(paths, version.Value, summary);
                    summary.Add(Strings.SetupPackageSaved + Environment.NewLine + package);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Keep the on-screen summary available even on a read-only disk.
                    LogDiagnosticsWriteFailure(logger, ex.HResult);
                }
                await wizard.WaitForRetryAsync(message, string.Join(Environment.NewLine, summary), stopping);
            }
        }
        finally
        {
            _running = false;
        }
    }

    public void RequestRepair() => dispatcher.Post(async () =>
    {
        try
        {
            await RunAsync(showWizard: true, lifetime.ApplicationStopping).ConfigureAwait(true);
            await main.RefreshAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (lifetime.ApplicationStopping.IsCancellationRequested)
        {
            // AWL closed during the repair.
        }
    });

    private void ShowWizard(bool visible)
    {
        window.Wizard.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        window.Workspace.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
    }

    [LoggerMessage(EventId = 1020, Level = LogLevel.Warning, Message = "Setup diagnostics could not be saved (HRESULT {HResult:X8}).")]
    private static partial void LogDiagnosticsWriteFailure(ILogger logger, int hResult);

    [LoggerMessage(EventId = 1019, Level = LogLevel.Information, Message = "Setup: {Summary}")]
    private static partial void LogSetup(ILogger logger, string summary);
}
