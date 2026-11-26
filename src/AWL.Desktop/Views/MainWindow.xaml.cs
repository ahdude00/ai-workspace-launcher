using System.Windows;
using AWL.Desktop.ViewModels;

namespace AWL.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly DropZoneViewModel _drop;

    internal SetupWizard Wizard { get; } = new() { Visibility = Visibility.Collapsed };

    public MainWindow(MainWindowViewModel viewModel, DropZoneViewModel drop)
    {
        InitializeComponent();
        Layout.Children.Add(Wizard);
        DataContext = viewModel;
        _drop = drop;
        DropZone.DataContext = drop;
    }

    // Files can be dropped anywhere on the window (BLG-07). Preview events run before text boxes claim the drop.
    private void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnPreviewDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            _drop.Drop(paths);
            e.Handled = true;
        }
    }
}
