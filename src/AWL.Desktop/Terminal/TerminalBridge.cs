using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using AWL.Core.Configuration;
using AWL.Core.Common;
using AWL.Core.Terminal;
using AWL.Desktop.Resources;
using AWL.Desktop.Views;
using AWL.Infrastructure.Terminal;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace AWL.Desktop.Terminal;

[SupportedOSPlatform("windows10.0.17763")]
internal sealed class TerminalBridge : IDisposable
{
    private const string Address = "https://terminal.awl.invalid/index.html";
    private readonly MainWindow _window;
    private readonly WebView2 _view = new();
    private readonly DockPanel _panel = new() { Background = System.Windows.Media.Brushes.Black };
    private readonly CancellationTokenSource _stop = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Channel<byte[]> _input = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleWriter = true, SingleReader = true });
    private TaskCompletionSource? _drained;
    private short _columns = 80, _rows = 24;
    private ConPtyHost? _host;
    private readonly Button _separate = new() { Content = Strings.OpenExternalButton, IsEnabled = false };
    private Window? _separateWindow;
    private bool _disposed;

    private TerminalBridge(MainWindow window, Guid sessionId, string title)
    {
        _window = window;
        var close = new Button { Content = Strings.CloseTerminal, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Dispose();
        _separate.Click += (_, _) => ShowSeparateWindow(title);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(_separate);
        actions.Children.Add(close);
        DockPanel.SetDock(actions, Dock.Top);
        _panel.Children.Add(actions);
        _panel.Children.Add(_view);
        System.Windows.Automation.AutomationProperties.SetName(_view, title);
        window.Closed += WindowClosed;
        _panel.Tag = sessionId;
        window.TerminalPane.Children.Add(_panel);
        window.UpdateTerminalSelection();
    }

    internal static async Task<Result?> TryOpenAsync(MainWindow window, Guid sessionId, ClaudeLaunchSpec spec, string title, CancellationToken token)
    {
        if (window.TerminalPane.Children.OfType<DockPanel>().Any(panel => Equals(panel.Tag, sessionId)))
        {
            return Result.Ok();
        }
        var bridge = new TerminalBridge(window, sessionId, title);
        try
        {
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(token, bridge._stop.Token);
            startup.CancelAfter(TimeSpan.FromSeconds(15));
            await bridge.InitializeAsync(startup.Token);
            startup.Token.ThrowIfCancellationRequested();
            if (bridge._panel.IsVisible)
            {
                bridge._view.Focus();
            }
            bridge._host = ConPtyHost.Start(spec, bridge._columns, bridge._rows);
            bridge._separate.IsEnabled = true;
            _ = bridge.RunAsync(bridge._host, bridge._stop.Token);
            return Result.Ok();
        }
        catch (Exception ex) when (ex is WebView2RuntimeNotFoundException or COMException or IOException or
            InvalidOperationException or Win32Exception or UnauthorizedAccessException or OperationCanceledException)
        {
            bool closed = bridge._disposed;
            bridge.Dispose();
            token.ThrowIfCancellationRequested();
            if (closed)
            {
                return Result.Fail(new AppError("TerminalLaunchFailed", "TerminalLaunchFailed"));
            }
            return null;
        }
    }

    private void ShowSeparateWindow(string title)
    {
        if (_separateWindow is { } existing)
        {
            existing.Activate();
            return;
        }

        var window = new Window { Owner = _window, Title = title, Width = 900, Height = 600 };
        _panel.Children.Remove(_view);
        window.Content = _view;
        _separateWindow = window;
        window.Closed += (_, _) =>
        {
            window.Content = null;
            _separateWindow = null;
            if (!_disposed)
            {
                _panel.Children.Add(_view);
            }
        };
        window.Show();
        _view.Focus();
    }

    private async Task InitializeAsync(CancellationToken token)
    {
        var environment = await CoreWebView2Environment.CreateAsync(null,
            Path.Combine(AwlPaths.FromEnvironment().CacheDirectory, "webview2")).WaitAsync(token);
        await _view.EnsureCoreWebView2Async(environment).WaitAsync(token);
        var core = _view.CoreWebView2;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreHostObjectsAllowed = false;
        core.Settings.AreDefaultScriptDialogsEnabled = false;
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.NavigationStarting += (_, e) => e.Cancel = e.Uri != Address;
        core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
        core.ProcessFailed += (_, _) => Dispose();
        core.WebMessageReceived += OnMessage;
        core.SetVirtualHostNameToFolderMapping("terminal.awl.invalid", Path.Combine(AppContext.BaseDirectory, "terminal"),
            CoreWebView2HostResourceAccessKind.DenyCors);
        core.Navigate(Address);
        await _ready.Task.WaitAsync(token);
    }

    private void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_disposed || e.Source != Address)
        {
            return;
        }
        using var json = JsonDocument.Parse(e.WebMessageAsJson);
        var message = json.RootElement;
        string? type = message.GetProperty("type").GetString();
        if (type is "ready" or "resize")
        {
            int cols = message.GetProperty("cols").GetInt32(), rows = message.GetProperty("rows").GetInt32();
            if (cols is > 0 and <= short.MaxValue && rows is > 0 and <= short.MaxValue)
            {
                _columns = (short)cols;
                _rows = (short)rows;
                try
                {
                    _host?.Resize(_columns, _rows);
                }
                catch (COMException)
                {
                    Dispose();
                    return;
                }
                if (type == "ready")
                {
                    _ready.TrySetResult();
                }
            }
        }
        else if (type == "drained")
        {
            _drained?.TrySetResult();
        }
        else if (type is "input" or "binary")
        {
            string data = message.GetProperty("data").GetString()!;
            if (!_input.Writer.TryWrite((type == "binary" ? Encoding.Latin1 : Encoding.UTF8).GetBytes(data)))
            {
                Dispose();
            }
        }
    }

    private async Task RunAsync(ConPtyHost host, CancellationToken token)
    {
        var read = Task.Run(() => TerminalOutput.PumpAsync(host.Output,
            data => _view.Dispatcher.InvokeAsync(() => SendAsync(data, token)).Task.Unwrap(), token), token);
        var write = Task.Run(async () =>
        {
            await foreach (byte[] bytes in _input.Reader.ReadAllAsync(token))
            {
                await host.Input.WriteAsync(bytes, token);
            }
        }, token);
        try
        {
            await Task.WhenAny(read, write);
        }
        finally
        {
            Dispose();
        }
        try
        {
            await Task.WhenAll(read, write);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException or COMException)
        {
            // Closing either endpoint stops both pumps.
        }
    }

    private async Task SendAsync(string data, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _view.CoreWebView2.PostWebMessageAsString(data);
        await _drained.Task.WaitAsync(token);
    }

    private void WindowClosed(object? sender, EventArgs e) => Dispose();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _window.Closed -= WindowClosed;
        _stop.Cancel();
        _input.Writer.TryComplete();
        _separateWindow?.Close();
        _view.Dispose();
        _window.TerminalPane.Children.Remove(_panel);
        _host?.Dispose();
        _stop.Dispose();
    }
}
