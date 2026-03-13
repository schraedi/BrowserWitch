using BrowserWitch.Browsers;
using BrowserWitch.Config;
using BrowserWitch.Routing;
using Microsoft.Win32;

namespace BrowserWitch.Tray;

public class TrayApplicationContext : ApplicationContext
{
    private const string StartupRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "BrowserWitch";

    private readonly NotifyIcon _notifyIcon;
    private readonly HotkeyWindow _hotkeyWindow;

    public TrayApplicationContext()
    {
        var contextMenu = new ContextMenuStrip();
        contextMenu.Opening += OnMenuOpening;
        contextMenu.Items.Add("Open URL from Clipboard\tCtrl+Shift+B", null, OnOpenFromClipboard);
        contextMenu.Items.Add("Recent URLs", null, OnShowRecentUrls);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Edit Config", null, OnEditConfig);
        contextMenu.Items.Add("Reload Config", null, OnReloadConfig);
        contextMenu.Items.Add(new ToolStripSeparator());

        var startupItem = new ToolStripMenuItem("Start with Windows")
        {
            Checked = IsStartupEnabled(),
            CheckOnClick = true
        };
        startupItem.CheckedChanged += OnStartupToggled;
        contextMenu.Items.Add(startupItem);

        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Register as Browser", null, OnRegister);
        contextMenu.Items.Add("Unregister", null, OnUnregister);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("About", null, OnAbout);
        contextMenu.Items.Add("Exit", null, OnExit);

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "BrowserWitch",
            Visible = true,
            ContextMenuStrip = contextMenu
        };

        // Register global hotkey: Ctrl+Shift+B
        _hotkeyWindow = new HotkeyWindow();
        _hotkeyWindow.HotkeyPressed += OnHotkeyPressed;
        if (!_hotkeyWindow.RegisterHotkey(GlobalHotkey.MOD_CONTROL | GlobalHotkey.MOD_SHIFT, Keys.B))
        {
            _notifyIcon.ShowBalloonTip(3000, "BrowserWitch",
                "Could not register Ctrl+Shift+B hotkey. It may be in use by another application.",
                ToolTipIcon.Warning);
        }
    }

    private static Icon LoadAppIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "icon.ico");
        if (File.Exists(iconPath))
            return new Icon(iconPath);

        iconPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
        if (File.Exists(iconPath))
            return new Icon(iconPath);

        return SystemIcons.Application;
    }

    private void OnMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (sender is not ContextMenuStrip menu) return;

        var clipboardItem = menu.Items[0];
        var url = GetClipboardUrl();
        clipboardItem.Enabled = url != null;
        clipboardItem.ToolTipText = url != null ? url : "No valid URL in clipboard";
    }

    private static string? GetClipboardUrl()
    {
        try
        {
            if (!Clipboard.ContainsText()) return null;
            var text = Clipboard.GetText().Trim();
            if (Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
                (uri.Scheme == "http" || uri.Scheme == "https"))
                return text;
        }
        catch { }
        return null;
    }

    private void OnShowRecentUrls(object? sender, EventArgs e)
    {
        var entries = RouteLog.GetEntries();
        if (entries.Count == 0)
        {
            MessageBox.Show("No URLs have been routed yet.",
                "BrowserWitch - Recent URLs",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var lines = entries
            .AsEnumerable()
            .Reverse()
            .Select(e =>
            {
                var line = $"[{e.Timestamp:HH:mm:ss}] → {e.BrowserKey}";
                line += $"\n  URL:      {e.OriginalUrl}";
                if (e.ResolvedUrl != e.OriginalUrl)
                    line += $"\n  Resolved: {e.ResolvedUrl}";
                if (e.CleanedUrl != e.ResolvedUrl)
                    line += $"\n  Cleaned:  {e.CleanedUrl}";
                return line;
            });

        var text = string.Join("\n\n", lines);

        // Use a simple form with a textbox for easy copying
        var form = new Form
        {
            Text = "BrowserWitch - Recent URLs",
            Width = 800,
            Height = 500,
            StartPosition = FormStartPosition.CenterScreen
        };

        var textBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9f),
            Text = text,
            WordWrap = true
        };

        form.Controls.Add(textBox);
        form.Show();
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        RouteFromClipboard();
    }

    private void OnOpenFromClipboard(object? sender, EventArgs e)
    {
        RouteFromClipboard();
    }

    private void RouteFromClipboard()
    {
        try
        {
            var url = GetClipboardUrl();
            if (url == null)
            {
                _notifyIcon.ShowBalloonTip(2000, "BrowserWitch",
                    "Clipboard does not contain a valid URL.", ToolTipIcon.Info);
                return;
            }

            var config = ConfigManager.Load();
            UrlRouter.Route(url, config);
        }
        catch (Exception ex)
        {
            _notifyIcon.ShowBalloonTip(3000, "BrowserWitch",
                $"Error: {ex.Message}", ToolTipIcon.Error);
        }
    }

    private void OnEditConfig(object? sender, EventArgs e)
    {
        ConfigManager.OpenInEditor();
    }

    private void OnReloadConfig(object? sender, EventArgs e)
    {
        try
        {
            var config = ConfigManager.Load();
            _notifyIcon.ShowBalloonTip(2000, "BrowserWitch",
                $"Config reloaded. {config.Rules.Count} rules, {config.Browsers.Count} browsers.",
                ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _notifyIcon.ShowBalloonTip(3000, "BrowserWitch",
                $"Config error: {ex.Message}",
                ToolTipIcon.Error);
        }
    }

    private void OnStartupToggled(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem item) return;

        if (item.Checked)
            EnableStartup();
        else
            DisableStartup();
    }

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, false);
        return key?.GetValue(StartupValueName) != null;
    }

    private static void EnableStartup()
    {
        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                      ?? Path.Combine(AppContext.BaseDirectory, "BrowserWitch.exe");

        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
        key?.SetValue(StartupValueName, $"\"{exePath}\"");
    }

    private static void DisableStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
        key?.DeleteValue(StartupValueName, false);
    }

    private void OnRegister(object? sender, EventArgs e)
    {
        Registration.BrowserRegistrar.Register();
    }

    private void OnUnregister(object? sender, EventArgs e)
    {
        Registration.BrowserRegistrar.Unregister();
    }

    private void OnAbout(object? sender, EventArgs e)
    {
        var config = ConfigManager.Load();
        var browserList = string.Join("\n",
            config.Browsers.Select(b => $"  {b.Key}: {b.Value.Name} ({b.Value.Path})"));

        var detected = BrowserDetector.Detect();
        var detectedList = string.Join("\n",
            detected.Select(b => $"  {b.Key}: {b.Name}"));

        MessageBox.Show(
            $"BrowserWitch\n\n" +
            $"Config: {ConfigManager.GetConfigPath()}\n" +
            $"Default browser: {config.DefaultBrowser}\n" +
            $"Rules: {config.Rules.Count}\n\n" +
            $"Configured browsers:\n{browserList}\n\n" +
            $"Detected browsers:\n{detectedList}\n\n" +
            $"Hotkey: Ctrl+Shift+B (open URL from clipboard)",
            "About BrowserWitch",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _hotkeyWindow.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hotkeyWindow.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
