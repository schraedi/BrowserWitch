using BrowserWitch.Config;
using BrowserWitch.Registration;
using BrowserWitch.Routing;
using BrowserWitch.Tray;

namespace BrowserWitch;

static class Program
{
    private const string MutexName = "BrowserWitch_SingleInstance_Mutex";

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (args.Length > 0)
        {
            var arg = args[0].ToLowerInvariant();

            switch (arg)
            {
                case "--register":
                    BrowserRegistrar.Register();
                    return;

                case "--unregister":
                    BrowserRegistrar.Unregister();
                    return;

                default:
                    // Assume it's a URL
                    if (arg.StartsWith("http://") || arg.StartsWith("https://") || arg.Contains("://"))
                    {
                        var config = ConfigManager.Load();
                        UrlRouter.Route(args[0], config); // Use original case URL
                        return;
                    }
                    break;
            }
        }

        // No args or unrecognized: run as tray application
        // Use a mutex to prevent multiple tray instances
        using var mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            // Another instance is already running
            return;
        }

        Application.Run(new TrayApplicationContext());
    }
}
