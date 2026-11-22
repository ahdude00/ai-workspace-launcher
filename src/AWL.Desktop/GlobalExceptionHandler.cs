using System.Diagnostics;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace AWL.Desktop;

internal sealed partial class GlobalExceptionHandler
{
    private readonly Dispatcher _dispatcher;
    private readonly ILogger _logger;
    private readonly Action _showError;

    public GlobalExceptionHandler(Dispatcher dispatcher, ILogger logger, Action showError)
    {
        _dispatcher = dispatcher;
        _logger = logger;
        _showError = showError;

        _dispatcher.UnhandledException += OnDispatcherException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainException;
    }

    public void LogStartupFailure(Exception exception) => LogFailure("Startup", exception, LogLevel.Critical);

    public void Detach()
    {
        _dispatcher.UnhandledException -= OnDispatcherException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException -= OnAppDomainException;
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        LogFailure("Dispatcher", e.Exception, LogLevel.Error);
        QueueError();
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        foreach (Exception exception in e.Exception.Flatten().InnerExceptions)
        {
            LogFailure("TaskScheduler", exception, LogLevel.Error);
        }

        QueueError();
    }

    private void OnAppDomainException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            LogFailure("AppDomain", exception, e.IsTerminating ? LogLevel.Critical : LogLevel.Error);
        }

        // The runtime can terminate the process immediately after this handler returns.
        if (!e.IsTerminating)
        {
            QueueError();
        }
    }

    private void QueueError()
    {
        if (!_dispatcher.HasShutdownStarted)
        {
            _dispatcher.BeginInvoke(DispatcherPriority.Normal, _showError);
        }
    }

    private void LogFailure(string origin, Exception exception, LogLevel level)
    {
        if (_logger.IsEnabled(level))
        {
            // Exception messages and source locations may hold sensitive data.
            string? exceptionType = exception.GetType().FullName;
            string stackFrames = new StackTrace(exception, fNeedFileInfo: false).ToString();
            LogUnexpectedException(_logger, level, origin, exceptionType, exception.HResult, stackFrames);
        }
    }

    [LoggerMessage(EventId = 1001, SkipEnabledCheck = true,
        Message = "Unexpected exception from {Origin}. Type: {ExceptionType}. HRESULT: 0x{HResult:X8}. Stack: {StackFrames}")]
    private static partial void LogUnexpectedException(
        ILogger logger, LogLevel level, string origin, string? exceptionType, int hResult, string stackFrames);
}
