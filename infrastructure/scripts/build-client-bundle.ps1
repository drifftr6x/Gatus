<#
.SYNOPSIS
    Builds the client installer bundle for Windows 11 Pro kiosk machines:
    self-contained agent + kiosk runtime, setup/uninstall scripts, README.
    Output: dist\GatusKiosk-Bundle-<version>.zip

    Server URL is auto-detected from the machine's primary IP (or pass -ServerUrl
    to override). Written to server-config.json in the bundle; setup.ps1 reads it
    when -ServerUrl is not passed explicitly.

    Enrollment token can be baked in with -EnrollmentToken (single-use — one
    bundle per machine). setup.ps1 reads it when -EnrollmentToken is not passed.

    .EXAMPLE
    .\build-client-bundle.ps1 -Version 1.1.0
    .\build-client-bundle.ps1 -Version 1.1.0 -ServerUrl http://192.168.1.100:5163
    .\build-client-bundle.ps1 -Version 1.1.0 -EnrollmentToken "gt_abc123..."  # zero-touch for one machine
    #>
    param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$OutDir = '',
    [string]$ServerUrl = '',
    [string]$EnrollmentToken = ''
    )

$ErrorActionPreference = 'Stop'
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$repoRoot = Resolve-Path (Join-Path $scriptDir '..\..')
if ([string]::IsNullOrEmpty($OutDir)) { $OutDir = Join-Path $repoRoot 'dist' }
$dotnet = "$env:USERPROFILE\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }

$bundleRoot = Join-Path $OutDir "bundle-$Version"
$agentOut = Join-Path $bundleRoot 'agent'
$runtimeOut = Join-Path $bundleRoot 'runtime'

if (Test-Path $bundleRoot) { Remove-Item $bundleRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $agentOut, $runtimeOut | Out-Null

Write-Host "==> Publishing agent (Release, win-x64, self-contained single-file, v$Version)"
& $dotnet publish (Join-Path $repoRoot 'agents\windows-agent\SentinelKiosk.Agent.csproj') `
    -c Release -r win-x64 --self-contained -o $agentOut `
    /p:Version=$Version /p:InformationalVersion=$Version /p:PublishSingleFile=true
if ($LASTEXITCODE -ne 0) { throw "agent publish failed" }

Write-Host "==> Publishing kiosk runtime (Release, win-x64, self-contained, v$Version)"
& $dotnet publish (Join-Path $repoRoot 'agents\windows-kiosk-runtime\SentinelKiosk.Runtime.csproj') `
    -c Release -r win-x64 --self-contained -o $runtimeOut `
    /p:Version=$Version /p:InformationalVersion=$Version
if ($LASTEXITCODE -ne 0) { throw "runtime publish failed" }

# Prune symbol files
Get-ChildItem $agentOut, $runtimeOut -Filter *.pdb | Remove-Item -Force

# Copy scripts + README
Copy-Item (Join-Path $repoRoot 'infrastructure\bundle\setup.ps1') $bundleRoot
Copy-Item (Join-Path $repoRoot 'infrastructure\bundle\uninstall.ps1') $bundleRoot
Copy-Item (Join-Path $repoRoot 'infrastructure\bundle\README.txt') $bundleRoot

# Stamp version into bundle marker
"Gatus Kiosk client bundle v$Version -- built $(Get-Date -Format 'yyyy-MM-dd HH:mm')" |
    Set-Content (Join-Path $bundleRoot 'VERSION.txt')

# Server URL config — auto-detect or use explicit parameter
if (-not $ServerUrl) {
    # Auto-detect: use the primary non-loopback IPv4 address
    $ip = (Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object { $_.InterfaceAlias -notmatch 'Loopback|Bluetooth|VMware|VirtualBox|Hyper-V|vEthernet' -and
                       $_.IPAddress -notmatch '^169\.254\.' -and
                       $_.IPAddress -ne '127.0.0.1' } |
        Sort-Object InterfaceMetric |
        Select-Object -First 1).IPAddress

    if ($ip) {
        $ServerUrl = "http://${ip}:5163"
        Write-Host "  Auto-detected server: $ServerUrl" -ForegroundColor Yellow
        Write-Host "  (Override with -ServerUrl if this is not correct)" -ForegroundColor Yellow
    }
    else {
        $ServerUrl = 'http://localhost:5163'
        Write-Host "  WARNING: Could not auto-detect IP. Using $ServerUrl" -ForegroundColor Red
    }
}

$config = @{ serverUrl = $ServerUrl; bundleVersion = $Version; builtAt = (Get-Date -Format 'o') }
if ($EnrollmentToken) {
    $config.enrollmentToken = $EnrollmentToken
    Write-Host "  Enrollment token baked in (single-use — this bundle works for ONE machine)" -ForegroundColor Yellow
}
$config | ConvertTo-Json | Set-Content (Join-Path $bundleRoot 'server-config.json') -Encoding utf8
Write-Host "  Server config written: $ServerUrl" -ForegroundColor Cyan

$zipPath = Join-Path $OutDir "GatusKiosk-Bundle-$Version.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $bundleRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal

$size = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host ""
Write-Host "Bundle: $zipPath ($size MB)" -ForegroundColor Green
Write-Host "Deploy: copy zip to client, extract, run as Administrator:"
if ($EnrollmentToken) {
    Write-Host "  .\setup.ps1                    # zero-touch (token + server embedded)"
} else {
    Write-Host "  .\setup.ps1 -EnrollmentToken <token>"
}
Write-Host "  (Server URL is embedded: $ServerUrl)"
