using System.Windows;
using AWL.Desktop.ViewModels;

namespace AWL.Desktop.Views;

public partial class AboutWindow : Window
{
    public AboutWindow(AboutViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
