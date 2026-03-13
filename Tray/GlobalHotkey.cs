using System.Runtime.InteropServices;

namespace BrowserWitch.Tray;

/// <summary>
/// Registers a system-wide hotkey using Win32 API.
/// Must be used from a thread with a message loop (WinForms UI thread).
/// </summary>
public class GlobalHotkey : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const int WM_HOTKEY = 0x0312;

    // Modifier keys
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_NOREPEAT = 0x4000;

    private readonly IntPtr _hWnd;
    private readonly int _id;
    private bool _registered;

    public GlobalHotkey(IntPtr windowHandle, int id, uint modifiers, Keys key)
    {
        _hWnd = windowHandle;
        _id = id;
        _registered = RegisterHotKey(_hWnd, _id, modifiers | MOD_NOREPEAT, (uint)key);
    }

    public bool IsRegistered => _registered;

    public void Dispose()
    {
        if (_registered)
        {
            UnregisterHotKey(_hWnd, _id);
            _registered = false;
        }
    }
}
