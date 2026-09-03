using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Platform.Api.Controllers;

/// <summary>
/// Generates a self-contained PowerShell deploy script with the enrollment
/// token and server URL baked in. The operator runs one line on the kiosk PC.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DeployController : ControllerBase
{
    private readonly ILogger<DeployController> _logger;

    public DeployController(ILogger<DeployController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Returns a PowerShell script that downloads the bundle from this server,
    /// extracts it, and runs setup.ps1 with the given token baked in.
    /// The script is self-contained — no other files needed on the client.
    /// </summary>
    /// <param name="token">The enrollment token to embed</param>
    /// <param name="serverUrl">Override the server URL (defaults to request host)</param>
    [HttpGet("script")]
    [AllowAnonymous] // The script itself contains a single-use token; no additional auth needed
    public IActionResult GetScript([FromQuery] string token, [FromQuery] string? serverUrl)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { error = "token query parameter is required" });

        // Default to the host the caller used
        var server = serverUrl ?? $"{Request.Scheme}://{Request.Host}";

        var script = GenerateDeployScript(server, token);

        _logger.LogInformation("Deploy script generated for server {Server}", server);

        return new ContentResult
        {
            Content = script,
            ContentType = "text/plain; charset=utf-8",
            StatusCode = 200
        };
    }

    /// <summary>
    /// Returns server address info (LAN IPs, host) so the UI can help the
    /// operator pick a reachable address instead of localhost.
    /// </summary>
    [HttpGet("server-info")]
    [AllowAnonymous]
    public IActionResult GetServerInfo()
    {
        var lanIps = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                     && n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                     && !a.Address.ToString().StartsWith("169.254."))
            .Select(a => a.Address.ToString())
            .Distinct()
            .OrderBy(ip => ip)
            .ToList();

        var port = Request.Host.Port ?? 5163;

        return Ok(new
        {
            requestHost = Request.Host.ToString(),
            requestScheme = Request.Scheme,
            port,
            hostName = Environment.MachineName,
            lanIps
        });
    }

    /// <summary>
    /// Returns the one-liner command the operator pastes on the kiosk PC.
    /// Shown on the admin UI deploy page.
    /// </summary>
    [HttpGet("command")]
    [Authorize(Policy = "RequireEditor")]
    public IActionResult GetCommand([FromQuery] string token, [FromQuery] string? serverUrl)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { error = "token query parameter is required" });

        var server = serverUrl ?? $"{Request.Scheme}://{Request.Host}";
        var encoded = Uri.EscapeDataString(token);

        // The one-liner: download script + execute
        var oneLiner = $"irm \"{server}/api/deploy/script?token={encoded}\" | iex";

        return Ok(new
        {
            command = oneLiner,
            scriptUrl = $"{server}/api/deploy/script?token={encoded}",
            serverUrl = server
        });
    }

    private static string GenerateDeployScript(string serverUrl, string token)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Gatus Kiosk — one-line deployment script");
        sb.AppendLine($"# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine($"# Server: {serverUrl}");
        sb.AppendLine("");
        sb.AppendLine("param([string]$WorkDir = \"$env:TEMP\\GatusKiosk-Deploy\")");
        sb.AppendLine("");
        sb.AppendLine("$ErrorActionPreference = 'Stop'");
        sb.AppendLine("[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12");
        sb.AppendLine("");
        sb.AppendLine("Write-Host '=== Gatus Kiosk Deployment ===' -ForegroundColor Cyan");
        sb.AppendLine($"Write-Host '  Server: {serverUrl}' -ForegroundColor White");
        sb.AppendLine("");
        sb.AppendLine("# ── Admin check ──────────────────────────────────────────────");
        sb.AppendLine("if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {");
        sb.AppendLine("    Write-Host 'ERROR: Run this script as Administrator' -ForegroundColor Red");
        sb.AppendLine("    Write-Host '  Right-click PowerShell → Run as Administrator, then paste again' -ForegroundColor Yellow");
        sb.AppendLine("    exit 1");
        sb.AppendLine("}");
        sb.AppendLine("");
        sb.AppendLine("# ── Download bundle ─────────────────────────────────────────");
        sb.AppendLine("$zipPath = Join-Path $WorkDir 'bundle.zip'");
        sb.AppendLine("$extractPath = Join-Path $WorkDir 'extracted'");
        sb.AppendLine("");
        sb.AppendLine("if (Test-Path $WorkDir) { Remove-Item $WorkDir -Recurse -Force }");
        sb.AppendLine("New-Item -ItemType Directory -Path $WorkDir -Force | Out-Null");
        sb.AppendLine("");
        sb.AppendLine($"Write-Host '  Downloading bundle from {serverUrl}/api/bundle/download ...' -ForegroundColor Yellow");
        sb.AppendLine($"Invoke-WebRequest -Uri '{serverUrl}/api/bundle/download' -OutFile $zipPath -UseBasicParsing");
        sb.AppendLine("");
        sb.AppendLine("$zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)");
        sb.AppendLine("Write-Host \"  Downloaded: $zipSize MB\" -ForegroundColor Green");
        sb.AppendLine("");
        sb.AppendLine("# ── Extract ──────────────────────────────────────────────────");
        sb.AppendLine("Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force");
        sb.AppendLine("");
        sb.AppendLine("# ── Write server config (overrides bundle defaults) ─────────");
        sb.AppendLine("$config = @{");
        sb.AppendLine($"    serverUrl = '{serverUrl}'");
        sb.AppendLine($"    enrollmentToken = '{token}'");
        sb.AppendLine("}");
        sb.AppendLine("$config | ConvertTo-Json | Set-Content (Join-Path $extractPath 'server-config.json') -Encoding utf8");
        sb.AppendLine("Write-Host '  Config written (server + token baked in)' -ForegroundColor Green");
        sb.AppendLine("");
        sb.AppendLine("# ── Run setup ────────────────────────────────────────────────");
        sb.AppendLine("$setupScript = Join-Path $extractPath 'setup.ps1'");
        sb.AppendLine("if (-not (Test-Path $setupScript)) {");
        sb.AppendLine("    Write-Host 'ERROR: setup.ps1 not found in bundle' -ForegroundColor Red");
        sb.AppendLine("    exit 1");
        sb.AppendLine("}");
        sb.AppendLine("");
        sb.AppendLine("Write-Host ''");
        sb.AppendLine("Write-Host '  Running setup...' -ForegroundColor Yellow");
        sb.AppendLine("& $setupScript");
        sb.AppendLine("");
        sb.AppendLine("Write-Host ''");
        sb.AppendLine("Write-Host '=== Deployment complete ===' -ForegroundColor Green");
        sb.AppendLine("Write-Host '  Check the admin UI dashboard to verify the device is online.' -ForegroundColor White");
        sb.AppendLine($"Write-Host '  Dashboard: {serverUrl.Replace(":5163", ":5173")}/dashboard' -ForegroundColor White");
        sb.AppendLine("");

        return sb.ToString();
    }
}
