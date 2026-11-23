using System.Windows;
using AWL.Desktop.ViewModels;

namespace AWL.Desktop.Views;

public partial class MainWindow : Window
{
    internal SetupWizard Wizard { get; } = new() { Visibility = Visibility.Collapsed };

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        Layout.Children.Add(Wizard);
        DataContext = viewModel;
    }
}
