using System.Runtime.InteropServices;

namespace WhisperFTPApp.Services;

public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = "Global\\AzioWhisperFTP_SingleInstance_Mutex";
    private Mutex? _mutex;
    private bool _hasHandle;

    public bool TryAcquire()
    {
        _mutex = new Mutex(false, MutexName, out _);

        try
        {
            _hasHandle = _mutex.WaitOne(0, false);
        }
        catch (AbandonedMutexException)
        {
            _hasHandle = true;
        }

        return _hasHandle;
    }

    public static void BringExistingInstanceToFront()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var handle = NativeMethods.FindWindow(null, "AzioWhisper FTP");
        if (handle != IntPtr.Zero)
        {
            NativeMethods.SetForegroundWindow(handle);
            if (NativeMethods.IsIconic(handle))
            {
                NativeMethods.ShowWindow(handle, NativeMethods.SW_RESTORE);
            }
        }
    }

    public void Dispose()
    {
        if (_mutex == null) return;

        if (_hasHandle)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
        _mutex = null;
    }

    private static class NativeMethods
    {
        public const int SW_RESTORE = 9;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
