using SentinelKiosk.Runtime.Models;
using Serilog;

namespace SentinelKiosk.Runtime.Services;

public class NavigationGuard
{
    private KioskConfiguration _config;

    public NavigationGuard(KioskConfiguration config)
    {
        _config = config;
    }

    public void UpdateConfiguration(KioskConfiguration config)
    {
        _config = config;
        Log.Information("Navigation guard updated with {AllowedCount} allowed, {BlockedCount} blocked patterns",
            config.AllowedUrls.Count, config.BlockedUrls.Count);
    }

    public bool IsAllowed(string uri)
    {
        if (string.IsNullOrEmpty(uri))
            return false;

        // If no restrictions, allow all
        if (_config.AllowedUrls.Count == 0 && _config.BlockedUrls.Count == 0)
            return true;

        // Check blocked list first (takes precedence)
        foreach (var pattern in _config.BlockedUrls)
        {
            if (MatchesPattern(uri, pattern))
            {
                Log.Debug("URL blocked by pattern {Pattern}: {Uri}", pattern, uri);
                return false;
            }
        }

        // If allowed list is empty, allow all (except blocked)
        if (_config.AllowedUrls.Count == 0)
            return true;

        // Check allowed list
        foreach (var pattern in _config.AllowedUrls)
        {
            if (MatchesPattern(uri, pattern))
            {
                return true;
            }
        }

        Log.Debug("URL not in allowlist: {Uri}", uri);
        return false;
    }

    private static bool MatchesPattern(string uri, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return false;

        // Exact match
        if (uri.Equals(pattern, StringComparison.OrdinalIgnoreCase))
            return true;

        // Wildcard matching (e.g., "https://*.example.com/*")
        if (pattern.Contains('*'))
        {
            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(
                uri, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        // Prefix match (e.g., "https://example.com" matches "https://example.com/page")
        if (uri.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
