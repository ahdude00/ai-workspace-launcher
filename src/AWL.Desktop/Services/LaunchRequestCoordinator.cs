using AWL.Core.Shell;
using AWL.Desktop.Resources;
using AWL.Desktop.ViewModels;

namespace AWL.Desktop.Services;

/// <summary>
/// Handles launch requests from the command line and from later AWL processes (flow ④, SAG-01…05): the window
/// comes forward at once; folder and file requests wait until setup has finished and are handled one at a time.
/// </summary>
public sealed class LaunchRequestCoordinator : IDisposable
{
    private readonly ILaunchRequestSource _source;
    private readonly MainWindowViewModel _main;
    private readonly IUiDispatcher _dispatcher;
    private readonly IWindowActivator _activator;
    private readonly IClipboardService _clipboard;
    private readonly IDialogService _dialogs;
    private readonly Queue<LaunchRequest> _pending = new();
    private bool _ready;
    private bool _busy;

    public LaunchRequestCoordinator(ILaunchRequestSource source, MainWindowViewModel main, IUiDispatcher dispatcher,
        IWindowActivator activator, IClipboardService clipboard, IDialogService dialogs)
    {
        _source = source;
        _main = main;
        _dispatcher = dispatcher;
        _activator = activator;
        _clipboard = clipboard;
        _dialogs = dialogs;
        _source.RequestReceived += OnRequestReceived;
    }

    /// <summary>Accepts a request on the UI thread.</summary>
    public async Task SubmitAsync(LaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _activator.Activate();
        if (request.Kind == LaunchKind.Activate)
        {
            return;
        }
        _pending.Enqueue(request);
        await DrainAsync().ConfigureAwait(true);
    }

    /// <summary>Called on the UI thread once setup has finished and the session list is loaded.</summary>
    public async Task MarkReadyAsync()
    {
        _ready = true;
        await DrainAsync().ConfigureAwait(true);
    }

    public void Dispose() => _source.RequestReceived -= OnRequestReceived;

    private void OnRequestReceived(object? sender, LaunchRequest request) => _dispatcher.Post(() => SubmitAsync(request));

    // A request that arrives while the new-session window is open waits for it instead of opening a second one.
    private async Task DrainAsync()
    {
        if (!_ready || _busy)
        {
            return;
        }
        _busy = true;
        try
        {
            while (_pending.TryDequeue(out var request))
            {
                bool copied = request.Kind == LaunchKind.File && _clipboard.TrySetText($"\"{request.Path}\"");
                await _main.NewSessionAsync(request.Folder).ConfigureAwait(true);
                if (copied)
                {
                    _dialogs.ShowInfo(Strings.FilePathCopied);
                }
            }
        }
        finally
        {
            _busy = false;
        }
    }
}
