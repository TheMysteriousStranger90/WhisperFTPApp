using System.Globalization;

namespace WhisperFTPApp.Helpers;

internal static class FileHelper
{
    private static readonly string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };

    public static string FormatFileSize(long bytes)
    {
        if (bytes < 0) return "0 B";

        int i = 0;
        double dblBytes = bytes;

        while (dblBytes >= 1024 && i < SizeSuffixes.Length - 1)
        {
            dblBytes /= 1024;
            i++;
        }

        return string.Format(CultureInfo.InvariantCulture, "{0:0.##} {1}", dblBytes, SizeSuffixes[i]);
    }

    public static string GetFileIcon(string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);

        return extension.ToUpperInvariant() switch
        {
            ".TXT" or ".LOG" or ".MD" => "📄",
            ".PDF" => "📕",
            ".DOC" or ".DOCX" => "📘",
            ".XLS" or ".XLSX" => "📗",
            ".PPT" or ".PPTX" => "📙",
            ".JPG" or ".JPEG" or ".PNG" or ".GIF" or ".BMP" or ".SVG" => "🖼️",
            ".MP3" or ".WAV" or ".FLAC" or ".AAC" => "🎵",
            ".MP4" or ".AVI" or ".MKV" or ".MOV" => "🎬",
            ".ZIP" or ".RAR" or ".7Z" or ".TAR" or ".GZ" => "📦",
            ".EXE" or ".MSI" => "⚙️",
            ".DLL" => "🔧",
            ".CS" or ".VB" => "💻",
            ".JS" or ".TS" => "📜",
            ".HTML" or ".HTM" => "🌐",
            ".CSS" => "🎨",
            ".JSON" or ".XML" => "📋",
            ".SQL" => "🗃️",
            _ => "📄"
        };
    }

    public static bool IsImageFile(string extension)
    {
        var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg", ".webp", ".ico"
        };
        return imageExtensions.Contains(extension);
    }

    public static bool IsMediaFile(string extension)
    {
        var mediaExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma",
            ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm"
        };
        return mediaExtensions.Contains(extension);
    }

    public static bool IsArchiveFile(string extension)
    {
        var archiveExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz"
        };
        return archiveExtensions.Contains(extension);
    }

    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "unnamed";

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

        return string.IsNullOrWhiteSpace(sanitized) ? "unnamed" : sanitized;
    }

    public static string GetUniqueFileName(string directory, string fileName)
    {
        var fullPath = Path.Combine(directory, fileName);

        if (!File.Exists(fullPath))
            return fullPath;

        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var counter = 1;

        while (File.Exists(fullPath))
        {
            fullPath = Path.Combine(directory, $"{nameWithoutExt} ({counter}){extension}");
            counter++;
        }

        return fullPath;
    }
}
