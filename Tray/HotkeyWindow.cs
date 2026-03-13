namespace BrowserWitch.Tray;

/// <summary>
/// Hidden window that receives global hotkey messages.
/// </summary>
public class HotkeyWindow : NativeWindow, IDisposable
{
    public event EventHandler? HotkeyPressed;

    private const int HOTKEY_ID = 1;
    private GlobalHotkey? _hotkey;

    public HotkeyWindow()
    {
        CreateHandle(new CreateParams());
    }

    public bool RegisterHotkey(uint modifiers, Keys key)
    {
        _hotkey = new GlobalHotkey(Handle, HOTKEY_ID, modifiers, key);
        return _hotkey.IsRegistered;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == GlobalHotkey.WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        _hotkey?.Dispose();
        DestroyHandle();
    }
}
