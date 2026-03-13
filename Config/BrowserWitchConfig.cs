namespace BrowserWitch.Config;

public class BrowserWitchConfig
{
    public string DefaultBrowser { get; set; } = "edge";
    /// <summary>
    /// "off" - never confirm, "unwrapped" - confirm only when resolve rules changed the URL, "always" - confirm every URL.
    /// </summary>
    public string Confirm { get; set; } = "off";
    public List<BlacklistEntry> Blacklist { get; set; } = new();
    public List<ResolveRule> Resolve { get; set; } = new();
    public CleanConfig Clean { get; set; } = new();
    public List<RoutingRule> Rules { get; set; } = new();
    public Dictionary<string, BrowserEntry> Browsers { get; set; } = new();
}

public class CleanConfig
{
    public List<string> StripParams { get; set; } = new();
    public List<SimplifyRule> Simplify { get; set; } = new();
    public List<RewriteRule> Rewrite { get; set; } = new();
}

public class RewriteRule
{
    /// <summary>Domain glob pattern to match.</summary>
    public string Match { get; set; } = "";
    /// <summary>Replacement host.</summary>
    public string Host { get; set; } = "";
}

public class SimplifyRule
{
    public string Match { get; set; } = "";
    /// <summary>
    /// Regex pattern matched against the URL path (and query). Capture groups can be referenced in replace.
    /// </summary>
    public string Pattern { get; set; } = "";
    /// <summary>
    /// Replacement path. Use $1, $2 etc. for capture group references.
    /// </summary>
    public string Replace { get; set; } = "";
}

public class ResolveRule
{
    public string Match { get; set; } = "";
    /// <summary>
    /// "queryParam" - extract URL from a query parameter.
    /// "redirect" - follow HTTP redirects to get the final URL.
    /// </summary>
    public string Method { get; set; } = "redirect";
    /// <summary>
    /// For "queryParam" method: the query parameter name containing the real URL.
    /// </summary>
    public string? Param { get; set; }
}

public class RoutingRule
{
    public string Match { get; set; } = "";
    public string Browser { get; set; } = "";
}

public class BlacklistEntry
{
    public string Match { get; set; } = "";
    public string Category { get; set; } = "Blocked";
}

public class BrowserEntry
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
}
