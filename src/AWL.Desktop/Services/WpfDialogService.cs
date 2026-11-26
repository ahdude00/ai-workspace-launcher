using System.IO;
using System.Windows;
using AWL.Desktop.Resources;
using AWL.Desktop.ViewModels;
using AWL.Desktop.Views;
using Microsoft.Win32;

namespace AWL.Desktop.Services;

internal sealed class WpfDialogService : IDialogService
{
    public void ShowError(string message) => Show(message, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void ShowInfo(string message) => Show(message, MessageBoxButton.OK, MessageBoxImage.Information);

    public bool Confirm(string message) =>
        Show(message, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public string? PickFolder(string? initialFolder)
    {
        var dialog = new OpenFolderDialog { Title = Strings.PickFolderTitle };
        if (initialFolder is not null && Directory.Exists(initialFolder))
        {
            dialog.InitialDirectory = initialFolder;
        }
        return dialog.ShowDialog(Owner) == true ? dialog.FolderName : null;
    }

    public bool ShowSessionEditor(SessionEditorViewModel editor)
    {
        var window = new SessionEditorWindow(editor);
        if (Owner is { } owner)
        {
            window.Owner = owner;
        }
        return window.ShowDialog() == true;
    }

    // The active window (for example an open editor) owns new dialogs; otherwise the main window does.
    private static Window? Owner =>
        Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
        ?? (Application.Current?.MainWindow is { IsVisible: true } main ? main : null);

    private static MessageBoxResult Show(string message, MessageBoxButton buttons, MessageBoxImage image) =>
        Owner is { } owner
            ? MessageBox.Show(owner, message, Strings.MainWindowTitle, buttons, image)
            : MessageBox.Show(message, Strings.MainWindowTitle, buttons, image);
}
