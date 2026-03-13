using System.Diagnostics;
using System.Text.RegularExpressions;
using BrowserWitch.Config;

namespace BrowserWitch.Routing;

public static class UrlRouter
{
    public static void Route(string url, BrowserWitchConfig config)
    {
        // 1. Resolve wrapped URLs (safelinks, shorteners)
        var resolvedUrl = UrlResolver.Resolve(url, config);
        // 2. Clean tracking params and simplify
        var cleanUrl = UrlCleaner.Clean(resolvedUrl, config);
        // 3. Route to browser
        var browserKey = MatchBrowser(cleanUrl, config);

        // Log the route
        RouteLog.Add(new RouteLogEntry
        {
            Timestamp = DateTime.Now,
            OriginalUrl = url,
            ResolvedUrl = resolvedUrl,
            CleanedUrl = cleanUrl,
            BrowserKey = browserKey
        });

        LaunchBrowser(cleanUrl, browserKey, config);
    }

    public static string MatchBrowser(string url, BrowserWitchConfig config)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return config.DefaultBrowser;

        var host = uri.Host;

        foreach (var rule in config.Rules)
        {
            if (GlobMatchesDomain(rule.Match, host))
                return rule.Browser;
        }

        return config.DefaultBrowser;
    }

    private static void LaunchBrowser(string url, string browserKey, BrowserWitchConfig config)
    {
        if (config.Browsers.TryGetValue(browserKey, out var browser) &&
            !string.IsNullOrEmpty(browser.Path))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = browser.Path,
                Arguments = url,
                UseShellExecute = false
            });
        }
        else
        {
            // Fallback: let Windows handle it via shell
            // This shouldn't cause a loop since we only get called when we ARE the default browser
            // As a safety measure, try the default browser entry
            var fallback = config.Browsers.Values.FirstOrDefault();
            if (fallback != null && !string.IsNullOrEmpty(fallback.Path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fallback.Path,
                    Arguments = url,
                    UseShellExecute = false
                });
            }
        }
    }

    internal static bool GlobMatchesDomain(string pattern, string host)
    {
        // Convert glob pattern to regex
        // *.example.com -> matches example.com and anything.example.com
        var escaped = Regex.Escape(pattern);

        // Replace escaped wildcard \* with regex
        // Leading *. means "optional subdomain prefix"
        if (escaped.StartsWith(@"\*\."))
        {
            escaped = @"(.+\.)?" + escaped.Substring(4);
        }

        // Any remaining * matches within a segment
        escaped = escaped.Replace(@"\*", @"[^.]*");

        var regex = new Regex("^" + escaped + "$", RegexOptions.IgnoreCase);
        return regex.IsMatch(host);
    }
}
