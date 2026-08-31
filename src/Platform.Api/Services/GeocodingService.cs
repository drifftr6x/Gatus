using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Platform.Api.Services;

/// <summary>
/// Geocodes location strings (city, state) to lat/lng using Nominatim (OpenStreetMap).
/// Caches results in-memory to avoid repeated API calls.
/// </summary>
public class GeocodingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GeocodingService> _logger;
    private readonly Dictionary<string, (double lat, double lng)?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _lock = new(1, 1);

    public GeocodingService(IHttpClientFactory httpClientFactory, ILogger<GeocodingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Extract "City, ST" from a group name like "Store 42 - Buford, GA" and geocode it.
    /// </summary>
    public async Task<(double lat, double lng)?> GeocodeFromGroupNameAsync(string? groupName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(groupName)) return null;

        // Extract city, state from patterns like "Store 42 - Buford, GA"
        var dashIdx = groupName.LastIndexOf(" - ", StringComparison.Ordinal);
        if (dashIdx < 0) return null;

        var location = groupName[(dashIdx + 3)..].Trim();
        if (string.IsNullOrWhiteSpace(location)) return null;

        return await GeocodeAsync(location, ct);
    }

    /// <summary>
    /// Geocode a location string like "Buford, GA" to lat/lng.
    /// </summary>
    public async Task<(double lat, double lng)?> GeocodeAsync(string location, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(location)) return null;

        // Check cache
        await _lock.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(location, out var cached))
                return cached;
        }
        finally { _lock.Release(); }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "GatUs-Kiosk-Platform/1.0");
            client.Timeout = TimeSpan.FromSeconds(10);

            var url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(location)}&format=json&limit=1&countrycodes=us";
            var response = await client.GetStringAsync(url, ct);

            using var doc = JsonDocument.Parse(response);
            var results = doc.RootElement;

            if (results.GetArrayLength() == 0)
            {
                _logger.LogWarning("Geocoding: no results for '{Location}'", location);
                await CacheResult(location, null);
                return null;
            }

            var first = results[0];
            var lat = double.Parse(first.GetProperty("lat").GetString()!);
            var lng = double.Parse(first.GetProperty("lon").GetString()!);

            _logger.LogInformation("Geocoded '{Location}' → {Lat}, {Lng}", location, lat, lng);
            var result = (lat, lng);
            await CacheResult(location, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Geocoding failed for '{Location}'", location);
            await CacheResult(location, null);
            return null;
        }
    }

    private async Task CacheResult(string key, (double lat, double lng)? value)
    {
        await _lock.WaitAsync();
        try { _cache[key] = value; }
        finally { _lock.Release(); }
    }
}
