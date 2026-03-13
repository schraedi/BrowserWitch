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
        // 3. Check blacklist
        var blacklistMatch = FindBlacklistMatch(cleanUrl, config);
        if (blacklistMatch != null)
        {
            RouteLog.Add(new RouteLogEntry
            {
                Timestamp = DateTime.Now,
                OriginalUrl = url,
                ResolvedUrl = resolvedUrl,
                CleanedUrl = cleanUrl,
                BrowserKey = $"BLOCKED ({blacklistMatch.Category})"
            });

            MessageBox.Show(
                $"URL blocked: {blacklistMatch.Category}\n\n{cleanUrl}",
                "BrowserWitch - Blocked",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        // 4. Route to browser
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

        // 5. Confirmation dialog based on config
        var wasResolved = resolvedUrl != url;
        var shouldConfirm = config.Confirm.ToLowerInvariant() switch
        {
            "always" => true,
            "unwrapped" => wasResolved,
            _ => false
        };

        if (shouldConfirm)
        {
            var browserName = config.Browsers.TryGetValue(browserKey, out var b) ? b.Name : browserKey;
            var title = FetchPageTitle(cleanUrl);
            var message = "";
            if (wasResolved && Uri.TryCreate(url, UriKind.Absolute, out var originalUri))
                message += $"Unwrapped from: {originalUri.Host}\n\n";
            if (title != null)
                message += $"{title}\n\n";
            message += $"Destination:\n{cleanUrl}\n\nBrowser: {browserName}";

            var result = MessageBox.Show(
                message,
                "BrowserWitch - Confirm Navigation",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);

            if (result != DialogResult.OK)
                return;
        }

        LaunchBrowser(cleanUrl, browserKey, config);
    }

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    // oEmbed endpoints for sites that bury <title> deep in JS-heavy pages
    private static readonly Dictionary<string, string> OEmbedProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        { "youtube.com", "https://www.youtube.com/oembed?format=json&url=" },
        { "youtu.be", "https://www.youtube.com/oembed?format=json&url=" },
        { "vimeo.com", "https://vimeo.com/api/oembed.json?url=" },
        { "twitter.com", "https://publish.twitter.com/oembed?url=" },
        { "x.com", "https://publish.twitter.com/oembed?url=" },
    };

    private static string? FetchPageTitle(string url)
    {
        try
        {
            // Try oEmbed first for known providers
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var host = uri.Host;
                foreach (var provider in OEmbedProviders)
                {
                    if (host.EndsWith(provider.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        var title = FetchOEmbedTitle(provider.Value + Uri.EscapeDataString(url));
                        if (title != null)
                            return title;
                        break;
                    }
                }
            }

            // Fall back to HTML scraping
            return FetchHtmlTitle(url);
        }
        catch
        {
            return null;
        }
    }

    private static string? FetchOEmbedTitle(string oEmbedUrl)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, oEmbedUrl);
            var response = HttpClient.Send(request);
            if (!response.IsSuccessStatusCode)
                return null;

            using var stream = response.Content.ReadAsStream();
            var json = System.Text.Json.JsonDocument.Parse(stream);
            if (json.RootElement.TryGetProperty("title", out var titleProp))
            {
                var title = titleProp.GetString()?.Trim();
                return string.IsNullOrEmpty(title) ? null : title;
            }
        }
        catch { }
        return null;
    }

    private static string? FetchHtmlTitle(string url)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            var response = HttpClient.Send(request, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
                return null;

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType != null && !contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
                return null;

            using var stream = response.Content.ReadAsStream();
            // Read up to 64KB - enough for most sites
            var buffer = new byte[65536];
            var totalRead = 0;
            while (totalRead < buffer.Length)
            {
                var read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
                if (read == 0) break;
                totalRead += read;
            }

            var html = System.Text.Encoding.UTF8.GetString(buffer, 0, totalRead);
            var match = Regex.Match(html, @"<title[^>]*>(.*?)</title>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (match.Success)
            {
                var title = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value).Trim();
                return string.IsNullOrEmpty(title) ? null : title;
            }
        }
        catch { }
        return null;
    }

    private static BlacklistEntry? FindBlacklistMatch(string url, BrowserWitchConfig config)
    {
        if (config.Blacklist.Count == 0)
            return null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        foreach (var entry in config.Blacklist)
        {
            var pattern = entry.Match;

            // If pattern looks like a domain-only pattern, match against host
            if (!pattern.Contains('/'))
            {
                if (GlobMatchesDomain(pattern, uri.Host))
                    return entry;
            }
            else
            {
                // Match against full URL (host + path + query)
                var hostAndPathAndQuery = uri.Host + uri.PathAndQuery;
                var escaped = Regex.Escape(pattern);
                if (escaped.StartsWith(@"\*\."))
                    escaped = @"(.+\.)?" + escaped.Substring(4);
                escaped = escaped.Replace(@"\*", @".*");
                if (Regex.IsMatch(hostAndPathAndQuery, "^" + escaped, RegexOptions.IgnoreCase))
                    return entry;
            }
        }

        return null;
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
