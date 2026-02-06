using Avalonia.Controls;
using WhisperFTPApp.ViewModels;

namespace WhisperFTPApp.Views;

public partial class MainWindow : Window
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
