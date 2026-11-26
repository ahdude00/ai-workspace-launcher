using System.IO;
using System.Windows;
using AWL.Desktop.Resources;
using AWL.Desktop.ViewModels;
using AWL.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace AWL.Desktop.Services;

internal sealed class WpfDialogService(IServiceProvider services) : IDialogService
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

    public void ShowCategoryManager(CategoryManagerViewModel manager)
    {
        var window = new CategoryManagerWindow(manager);
        if (Owner is { } owner)
        {
            window.Owner = owner;
        }
        window.ShowDialog();
    }

    public string? PickSaveFile(string suggestedName, string filter)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedName,
            Filter = filter,
            AddExtension = true,
            OverwritePrompt = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public void ShowAbout()
    {
        var window = new AboutWindow(services.GetRequiredService<AboutViewModel>());
        if (Owner is { } owner)
        {
            window.Owner = owner;
        }
        window.ShowDialog();
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
