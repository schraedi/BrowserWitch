using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;

namespace BrowserWitch.Registration;

public static class BrowserRegistrar
{
    private const string AppName = "BrowserWitch";
    private const string ProgId = "BrowserWitchURL";

    private static string ExePath => Process.GetCurrentProcess().MainModule?.FileName
                                     ?? Path.Combine(AppContext.BaseDirectory, "BrowserWitch.exe");

    public static void Register()
    {
        if (!IsElevated())
        {
            RelaunchElevated("--register");
            return;
        }

        var quotedExe = $"\"{ExePath}\"";

        // Register as a StartMenuInternet client
        using (var key = Registry.LocalMachine.CreateSubKey($@"SOFTWARE\Clients\StartMenuInternet\{AppName}"))
        {
            key.SetValue(null, AppName);

            using (var caps = key.CreateSubKey("Capabilities"))
            {
                caps.SetValue("ApplicationName", AppName);
                caps.SetValue("ApplicationDescription", "Routes URLs to different browsers based on domain rules");
                caps.SetValue("ApplicationIcon", $"{ExePath},0");

                using (var urlAssoc = caps.CreateSubKey("URLAssociations"))
                {
                    urlAssoc.SetValue("http", ProgId);
                    urlAssoc.SetValue("https", ProgId);
                }
            }

            using (var shellCmd = key.CreateSubKey(@"shell\open\command"))
            {
                shellCmd.SetValue(null, $"{quotedExe} \"%1\"");
            }
        }

        // Register in RegisteredApplications
        using (var regApps = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\RegisteredApplications", true))
        {
            regApps?.SetValue(AppName, $@"SOFTWARE\Clients\StartMenuInternet\{AppName}\Capabilities");
        }

        // Register the ProgId in HKLM
        RegisterProgId(Registry.LocalMachine);
        // Also in HKCU for per-user association
        RegisterProgId(Registry.CurrentUser);

        MessageBox.Show(
            "BrowserWitch has been registered.\n\n" +
            "Now go to:\n" +
            "  Settings > Default Apps\n" +
            "and select BrowserWitch as your web browser.",
            "BrowserWitch - Registered",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    public static void Unregister()
    {
        if (!IsElevated())
        {
            RelaunchElevated("--unregister");
            return;
        }

        try
        {
            Registry.LocalMachine.DeleteSubKeyTree($@"SOFTWARE\Clients\StartMenuInternet\{AppName}", false);
            Registry.LocalMachine.DeleteSubKeyTree($@"SOFTWARE\Classes\{ProgId}", false);
            Registry.CurrentUser.DeleteSubKeyTree($@"SOFTWARE\Classes\{ProgId}", false);

            using var regApps = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\RegisteredApplications", true);
            regApps?.DeleteValue(AppName, false);

            MessageBox.Show(
                "BrowserWitch has been unregistered.",
                "BrowserWitch - Unregistered",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error during unregistration:\n{ex.Message}",
                "BrowserWitch - Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void RegisterProgId(RegistryKey root)
    {
        using var key = root.CreateSubKey($@"SOFTWARE\Classes\{ProgId}");
        key.SetValue(null, "BrowserWitch URL");
        key.SetValue("URL Protocol", "");

        using var icon = key.CreateSubKey("DefaultIcon");
        icon.SetValue(null, $"{ExePath},0");

        using var shellCmd = key.CreateSubKey(@"shell\open\command");
        shellCmd.SetValue(null, $"\"{ExePath}\" \"%1\"");
    }

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void RelaunchElevated(string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ExePath,
                Arguments = args,
                Verb = "runas",
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception)
        {
            MessageBox.Show(
                "Administrator privileges are required for registration.\nPlease approve the UAC prompt.",
                "BrowserWitch",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
