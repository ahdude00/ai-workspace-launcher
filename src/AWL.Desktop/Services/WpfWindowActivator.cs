using System.Windows;

namespace AWL.Desktop.Services;

internal sealed class WpfWindowActivator : IWindowActivator
{
    public void Activate()
    {
        if (Application.Current?.MainWindow is not { } window)
        {
            return;
        }
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }
        window.Show();
        window.Activate();
        // Toggling Topmost raises the window even when Windows refuses to move the focus.
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }
}
