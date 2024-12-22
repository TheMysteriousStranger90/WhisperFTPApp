using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WhisperFTPApp.ViewModels;

namespace WhisperFTPApp.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}