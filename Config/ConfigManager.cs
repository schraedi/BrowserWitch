using System.Diagnostics;
using System.Text.Json;
using BrowserWitch.Browsers;

namespace BrowserWitch.Config;

public static class ConfigManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static string GetConfigPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "browserswitch.json");
    }

    public static BrowserWitchConfig Load()
    {
        var path = GetConfigPath();
        if (!File.Exists(path))
            return EnsureConfig();

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<BrowserWitchConfig>(json, JsonOptions)
               ?? new BrowserWitchConfig();
    }

    public static void Save(BrowserWitchConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(GetConfigPath(), json);
    }

    public static BrowserWitchConfig EnsureConfig()
    {
        var detected = BrowserDetector.Detect();
        var config = new BrowserWitchConfig
        {
            Browsers = detected.ToDictionary(b => b.Key, b => new BrowserEntry
            {
                Name = b.Name,
                Path = b.ExePath
            })
        };

        // Pick a sensible default
        if (config.Browsers.ContainsKey("chrome"))
            config.DefaultBrowser = "chrome";
        else if (config.Browsers.ContainsKey("firefox"))
            config.DefaultBrowser = "firefox";
        else if (config.Browsers.ContainsKey("edge"))
            config.DefaultBrowser = "edge";
        else if (config.Browsers.Count > 0)
            config.DefaultBrowser = config.Browsers.Keys.First();

        // Blacklist known nuisance URLs
        config.Blacklist = new List<BlacklistEntry>
        {
            new() { Match = "*.youtube.com/watch?v=dQw4w9WgXcQ", Category = "Rickroll" },
            new() { Match = "youtu.be/dQw4w9WgXcQ", Category = "Rickroll" },
            new() { Match = "goatse.*", Category = "Shock site" },
            new() { Match = "lemonparty.*", Category = "Shock site" },
            new() { Match = "tubgirl.*", Category = "Shock site" },
            new() { Match = "meatspin.*", Category = "Shock site" }
        };

        // Add default URL cleaning rules
        config.Clean = new CleanConfig
        {
            StripParams = new List<string>
            {
                // Facebook / Meta
                "fbclid",
                // Google
                "gclid", "gclsrc", "dclid",
                // UTM tracking (used by everyone)
                "utm_source", "utm_medium", "utm_campaign", "utm_content", "utm_term", "utm_id",
                // Microsoft / Bing
                "msclkid",
                // Mailchimp
                "mc_cid", "mc_eid",
                // HubSpot
                "_hsenc", "_hsmi",
                // Adobe
                "s_cid",
                // Misc
                "ref_", "ref_src", "ref_url",
                "_openstat", "yclid", "wickedid", "twclid"
            },
            Rewrite = new List<RewriteRule>
            {
                new() { Match = "x.com", Host = "xcancel.com" },
                new() { Match = "*.x.com", Host = "xcancel.com" },
                new() { Match = "twitter.com", Host = "xcancel.com" },
                new() { Match = "*.twitter.com", Host = "xcancel.com" }
            },
            Simplify = new List<SimplifyRule>
            {
                new()
                {
                    Match = "*.youtube.com",
                    Pattern = @"/watch\?v=([a-zA-Z0-9_-]{11}).*",
                    Replace = "/watch?v=$1"
                },
                new()
                {
                    Match = "*.amazon.*",
                    Pattern = @".*/dp/([A-Z0-9]{10}).*",
                    Replace = "/dp/$1"
                },
                new()
                {
                    Match = "*.amazon.*",
                    Pattern = @".*/gp/product/([A-Z0-9]{10}).*",
                    Replace = "/dp/$1"
                }
            }
        };

        // Add default resolve rules for common URL wrappers
        config.Resolve = new List<ResolveRule>
        {
            new() { Match = "safelinks.protection.outlook.com", Method = "queryParam", Param = "url" },
            new() { Match = "*.safelinks.protection.outlook.com", Method = "queryParam", Param = "url" },
            new() { Match = "bit.ly", Method = "redirect" },
            new() { Match = "t.co", Method = "redirect" },
            new() { Match = "tinyurl.com", Method = "redirect" },
            new() { Match = "go.microsoft.com", Method = "redirect" },
            new() { Match = "statics.teams.cdn.office.net", Method = "queryParam", Param = "url" }
        };

        // Add example rules
        config.Rules = new List<RoutingRule>
        {
            new() { Match = "*.azure.com", Browser = "edge" },
            new() { Match = "portal.office.com", Browser = "edge" }
        };

        Save(config);
        return config;
    }

    public static void OpenInEditor()
    {
        var path = GetConfigPath();
        if (!File.Exists(path))
            EnsureConfig();

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
