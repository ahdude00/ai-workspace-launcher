using System.Windows;
using AWL.Desktop.ViewModels;

namespace AWL.Desktop.Views;

public partial class CategoryManagerWindow : Window
{
    public CategoryManagerWindow(CategoryManagerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
