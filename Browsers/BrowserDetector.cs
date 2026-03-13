using Microsoft.Win32;

namespace BrowserWitch.Browsers;

public static class BrowserDetector
{
    public static List<BrowserInfo> Detect()
    {
        var browsers = new List<BrowserInfo>();
        var exeLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var exeName = Path.GetFileNameWithoutExtension(exeLocation);

        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Clients\StartMenuInternet");
        if (key == null)
            return browsers;

        foreach (var subKeyName in key.GetSubKeyNames())
        {
            using var subKey = key.OpenSubKey(subKeyName);
            if (subKey == null) continue;

            var name = subKey.GetValue(null)?.ToString() ?? subKeyName;

            using var commandKey = subKey.OpenSubKey(@"shell\open\command");
            var command = commandKey?.GetValue(null)?.ToString();
            if (string.IsNullOrEmpty(command)) continue;

            // Strip surrounding quotes from the exe path
            var exePath = command.Trim('"');
            // Handle paths like "path.exe" --args
            var quoteEnd = command.IndexOf('"', 1);
            if (command.StartsWith('"') && quoteEnd > 0)
                exePath = command.Substring(1, quoteEnd - 1);

            // Skip ourselves
            if (exePath.Contains(exeName ?? "BrowserWitch", StringComparison.OrdinalIgnoreCase))
                continue;

            var browserKey = DeriveBrowserKey(subKeyName, name);

            browsers.Add(new BrowserInfo
            {
                Key = browserKey,
                Name = name,
                ExePath = exePath
            });
        }

        return browsers;
    }

    private static string DeriveBrowserKey(string registryName, string displayName)
    {
        // Common browser mappings
        if (displayName.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
            return "firefox";
        if (displayName.Contains("Google Chrome", StringComparison.OrdinalIgnoreCase))
            return "chrome";
        if (displayName.Contains("Edge", StringComparison.OrdinalIgnoreCase))
            return "edge";
        if (displayName.Contains("Brave", StringComparison.OrdinalIgnoreCase))
            return "brave";
        if (displayName.Contains("Vivaldi", StringComparison.OrdinalIgnoreCase))
            return "vivaldi";
        if (displayName.Contains("Opera", StringComparison.OrdinalIgnoreCase))
            return "opera";

        // Fallback: slugify the registry name
        return registryName
            .Split('-')[0]  // Remove hash suffixes like Firefox-308046B0AF4A39CB
            .ToLowerInvariant()
            .Replace(" ", "");
    }
}
