using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WhisperFTPApp.Views;

public partial class ScanView : UserControl
{
    public ScanView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}