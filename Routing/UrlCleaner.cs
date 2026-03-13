using System.Text.RegularExpressions;
using System.Web;
using BrowserWitch.Config;

namespace BrowserWitch.Routing;

public static class UrlCleaner
{
    /// <summary>
    /// Cleans a URL by stripping tracking parameters and applying simplification rules.
    /// </summary>
    public static string Clean(string url, BrowserWitchConfig config)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        url = StripTrackingParams(uri, config.Clean.StripParams);
        url = ApplyRewriteRules(url, config);
        url = ApplySimplifyRules(url, config);

        return url;
    }

    private static string StripTrackingParams(Uri uri, List<string> stripParams)
    {
        if (stripParams.Count == 0 || string.IsNullOrEmpty(uri.Query))
            return uri.AbsoluteUri;

        var query = HttpUtility.ParseQueryString(uri.Query);
        var stripped = false;

        foreach (var param in stripParams)
        {
            if (query[param] != null)
            {
                query.Remove(param);
                stripped = true;
            }
        }

        if (!stripped)
            return uri.AbsoluteUri;

        // Rebuild URL without the stripped params
        var builder = new UriBuilder(uri);
        builder.Query = query.Count > 0 ? query.ToString() : "";
        return builder.Uri.AbsoluteUri;
    }

    private static string ApplyRewriteRules(string url, BrowserWitchConfig config)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        foreach (var rule in config.Clean.Rewrite)
        {
            if (UrlRouter.GlobMatchesDomain(rule.Match, uri.Host))
            {
                var builder = new UriBuilder(uri) { Host = rule.Host };
                return builder.Uri.AbsoluteUri;
            }
        }

        return url;
    }

    private static string ApplySimplifyRules(string url, BrowserWitchConfig config)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        foreach (var rule in config.Clean.Simplify)
        {
            if (!UrlRouter.GlobMatchesDomain(rule.Match, uri.Host))
                continue;

            // Match the pattern against path + query
            var pathAndQuery = uri.PathAndQuery;
            var match = Regex.Match(pathAndQuery, rule.Pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var newPathAndQuery = Regex.Replace(pathAndQuery, rule.Pattern, rule.Replace, RegexOptions.IgnoreCase);
                // Split on '?' so UriBuilder doesn't encode it as part of the path
                var qIndex = newPathAndQuery.IndexOf('?');
                var builder = new UriBuilder(uri) { Fragment = "" };
                if (qIndex >= 0)
                {
                    builder.Path = newPathAndQuery.Substring(0, qIndex);
                    builder.Query = newPathAndQuery.Substring(qIndex + 1);
                }
                else
                {
                    builder.Path = newPathAndQuery;
                    builder.Query = "";
                }
                return builder.Uri.AbsoluteUri;
            }
        }

        return url;
    }
}
