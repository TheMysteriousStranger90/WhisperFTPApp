namespace WhisperFTPApp.Models.Navigations;

public sealed class NavigationItem
{
    public string Path { get; }
    public string Display { get; }

    public NavigationItem(string path, string display)
    {
        Path = path;
        Display = display;
    }
}
