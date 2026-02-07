using Avalonia.Controls;
using WhisperFTPApp.ViewModels;

namespace WhisperFTPApp.Views;

internal sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
