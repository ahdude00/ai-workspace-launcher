using AWL.Desktop.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AWL.Desktop.ViewModels;

/// <summary>
/// View model of the main window. Holds no WPF types (MVVM rule, Teknik Tasarım §08).
/// </summary>
public sealed class MainWindowViewModel : ObservableObject
{
    public string Title { get; } = Strings.MainWindowTitle;

    public string WelcomeText { get; } = Strings.MainWindowWelcome;
}
