using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace Platform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireViewer")]
public class LogsController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public LogsController(IWebHostEnvironment env)
    {
        _env = env;
    }

    /// <summary>Get recent log entries from the API server logs.</summary>
    [HttpGet]
    public ActionResult<LogResponse> GetLogs(
        [FromQuery] string? level = null,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 200,
        [FromQuery] int? lastMinutes = null,
        [FromQuery] string? source = null)
    {
        var logsDir = Path.Combine(_env.ContentRootPath, "logs");
        if (!Directory.Exists(logsDir))
            return Ok(new LogResponse([], 0));

        // Find the most recent log file (or user actions log)
          var pattern = source == "audit" ? "user-actions-*.json" : "log-*.json";
          var logFile = Directory.GetFiles(logsDir, pattern)
              .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc)
              .FirstOrDefault();

        if (logFile == null)
            return Ok(new LogResponse([], 0));

        // Read last N lines efficiently
        var lines = ReadLastLines(logFile, Math.Max(limit * 3, 1000)); // Read extra to account for filtering
        var entries = new List<LogEntry>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(line);
                var root = doc.RootElement;

                var entry = new LogEntry
                {
                    Timestamp = root.TryGetProperty("@t", out var t) ? t.GetString() ?? "" : "",
                    Level = root.TryGetProperty("@l", out var l) ? l.GetString() ?? "Information" : "Information",
                    Message = root.TryGetProperty("@mt", out var mt) ? RenderMessage(root) : "",
                    Exception = root.TryGetProperty("@x", out var x) ? x.GetString() : null,
                    CorrelationId = root.TryGetProperty("CorrelationId", out var cid) ? cid.GetString() : null,
                    RequestPath = root.TryGetProperty("RequestPath", out var rp) ? rp.GetString() : null,
                    StatusCode = root.TryGetProperty("StatusCode", out var sc) ? sc.GetInt32() : null,
                    Elapsed = root.TryGetProperty("Elapsed", out var el) ? el.GetDouble() : null,
                    Source = root.TryGetProperty("SourceContext", out var src) ? src.GetString() : null,
                };

                // Level filter
                if (!string.IsNullOrEmpty(level) && !entry.Level.Equals(level, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Time filter
                if (lastMinutes.HasValue && DateTime.TryParse(entry.Timestamp, out var ts))
                {
                    if (ts < DateTime.UtcNow.AddMinutes(-lastMinutes.Value))
                        continue;
                }

                // Text search
                if (!string.IsNullOrEmpty(search))
                {
                    var s = search.ToLowerInvariant();
                    if (!entry.Message.ToLowerInvariant().Contains(s) &&
                        !(entry.Exception?.ToLowerInvariant().Contains(s) ?? false) &&
                        !(entry.RequestPath?.ToLowerInvariant().Contains(s) ?? false) &&
                        !(entry.Source?.ToLowerInvariant().Contains(s) ?? false))
                        continue;
                }

                entries.Add(entry);
            }
            catch
            {
                // Skip malformed lines
            }
        }

        // Return most recent first, limited
        var result = entries.OrderByDescending(e => e.Timestamp).Take(limit).ToList();
        return Ok(new LogResponse(result, entries.Count));
    }

    /// <summary>Get available log levels for filter dropdown.</summary>
    [HttpGet("levels")]
    public ActionResult<string[]> GetLevels()
    {
        return Ok(new[] { "Verbose", "Debug", "Information", "Warning", "Error", "Fatal" });
    }

    private static string RenderMessage(System.Text.Json.JsonElement root)
    {
        var mt = root.TryGetProperty("@mt", out var mtEl) ? mtEl.GetString() ?? "" : "";

        // Simple property substitution for common properties
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name.StartsWith('@')) continue;
            var placeholder = $"{{{prop.Name}}}";
            if (mt.Contains(placeholder))
            {
                var value = prop.Value.ValueKind == System.Text.Json.JsonValueKind.String
                    ? prop.Value.GetString() ?? ""
                    : prop.Value.ToString();
                mt = mt.Replace(placeholder, value);
            }
        }

        return mt;
    }

    private static List<string> ReadLastLines(string path, int maxLines)
    {
        // Read from end of file for efficiency
        const int bufferSize = 4096;
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        var lines = new List<string>();
        var remaining = fs.Length;
        var leftover = "";

        while (remaining > 0 && lines.Count < maxLines)
        {
            var readSize = (int)Math.Min(bufferSize, remaining);
            remaining -= readSize;
            fs.Position = remaining;

            var buffer = new byte[readSize];
            fs.ReadExactly(buffer, 0, readSize);
            var chunk = System.Text.Encoding.UTF8.GetString(buffer) + leftover;

            var chunkLines = chunk.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (chunkLines.Length > 0 && !chunk.EndsWith('\n'))
            {
                leftover = chunkLines[0];
                lines.AddRange(chunkLines.Skip(1).Reverse());
            }
            else
            {
                leftover = "";
                lines.AddRange(chunkLines.Reverse());
            }
        }

        if (!string.IsNullOrEmpty(leftover))
            lines.Add(leftover);

        return lines;
    }
}

public record LogEntry
{
    public string Timestamp { get; init; } = "";
    public string Level { get; init; } = "Information";
    public string Message { get; init; } = "";
    public string? Exception { get; init; }
    public string? CorrelationId { get; init; }
    public string? RequestPath { get; init; }
    public int? StatusCode { get; init; }
    public double? Elapsed { get; init; }
    public string? Source { get; init; }
}

public record LogResponse(List<LogEntry> Entries, int TotalMatched);
