using System.Web;
using BrowserWitch.Config;

namespace BrowserWitch.Routing;

public static class UrlResolver
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("BrowserWitch/1.0");
        return client;
    }

    /// <summary>
    /// Resolves a URL through any matching resolve rules (safelinks, shorteners, etc.).
    /// Returns the final resolved URL, or the original if no rules matched.
    /// </summary>
    public static string Resolve(string url, BrowserWitchConfig config)
    {
        // Allow chained resolves (e.g. safelink -> shortener -> real URL)
        // but cap iterations to prevent infinite loops
        const int maxDepth = 5;

        for (int i = 0; i < maxDepth; i++)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                break;

            var rule = FindMatchingRule(uri.Host, config);
            if (rule == null)
                break;

            var resolved = rule.Method.ToLowerInvariant() switch
            {
                "queryparam" => ResolveQueryParam(uri, rule.Param ?? "url"),
                "redirect" => ResolveRedirect(url),
                _ => null
            };

            if (resolved == null || resolved == url)
                break;

            url = resolved;
        }

        return url;
    }

    private static ResolveRule? FindMatchingRule(string host, BrowserWitchConfig config)
    {
        foreach (var rule in config.Resolve)
        {
            if (UrlRouter.GlobMatchesDomain(rule.Match, host))
                return rule;
        }
        return null;
    }

    private static string? ResolveQueryParam(Uri uri, string paramName)
    {
        var query = HttpUtility.ParseQueryString(uri.Query);
        var value = query[paramName];

        if (string.IsNullOrEmpty(value))
            return null;

        // The value should be a URL (possibly URL-encoded)
        var decoded = Uri.UnescapeDataString(value);

        // Validate it's actually a URL
        if (Uri.TryCreate(decoded, UriKind.Absolute, out _))
            return decoded;

        return null;
    }

    private static string? ResolveRedirect(string url)
    {
        try
        {
            // Use HEAD first (faster), fall back to GET if HEAD not supported
            var resolved = FollowRedirects(url, HttpMethod.Head)
                        ?? FollowRedirects(url, HttpMethod.Get);
            return resolved;
        }
        catch
        {
            return null;
        }
    }

    private static string? FollowRedirects(string url, HttpMethod method)
    {
        const int maxRedirects = 10;
        var current = url;

        for (int i = 0; i < maxRedirects; i++)
        {
            try
            {
                var request = new HttpRequestMessage(method, current);
                var response = HttpClient.Send(request);

                if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
                {
                    var location = response.Headers.Location;
                    if (location == null)
                        break;

                    // Handle relative redirects
                    current = location.IsAbsoluteUri
                        ? location.AbsoluteUri
                        : new Uri(new Uri(current), location).AbsoluteUri;
                    continue;
                }

                // Not a redirect - we've arrived
                return current == url ? null : current;
            }
            catch
            {
                return null;
            }
        }

        return current == url ? null : current;
    }
}
