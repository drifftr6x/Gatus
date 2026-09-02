<#
.SYNOPSIS
    Builds the client installer bundle for Windows 11 Pro kiosk machines:
    self-contained agent + kiosk runtime, setup/uninstall scripts, README.
    Output: dist\GatusKiosk-Bundle-<version>.zip

    The bundle contains NO server URL or enrollment token -- those are
    per-deployment parameters passed to setup.ps1 on the client.

.EXAMPLE
    .\build-client-bundle.ps1 -Version 1.0.0
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$OutDir = ''
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

$zipPath = Join-Path $OutDir "GatusKiosk-Bundle-$Version.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $bundleRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal

$size = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host ""
Write-Host "Bundle: $zipPath ($size MB)" -ForegroundColor Green
Write-Host "Deploy: copy zip to client, extract, run as Administrator:"
Write-Host "  .\setup.ps1 -ServerUrl https://<your-server> -EnrollmentToken <token>"
