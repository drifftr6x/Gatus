<#
.SYNOPSIS
    Builds the Windows agent in Release and packages it as an update zip for upload
    via POST /api/agent-updates (admin UI or curl). The SERVER signs the manifest at
    upload time — this script never touches the signing private key.

.PARAMETER Version
    Version to stamp into the build (csproj <Version>) and the package, e.g. 1.1.0

.EXAMPLE
    .\publish-agent-update.ps1 -Version 1.1.0
    # then upload dist\agent-update-1.1.0.zip via the API
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$agentProj = Join-Path $repoRoot 'agents\windows-agent\SentinelKiosk.Agent.csproj'
$outDir = Join-Path $repoRoot "agents\windows-agent\publish\update-$Version"
$distDir = Join-Path $repoRoot 'dist'

$dotnet = "$env:USERPROFILE\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }

Write-Host "Building agent $Version (Release, win-x64, single-file)..."
& $dotnet publish $agentProj -c Release -r win-x64 -o $outDir `
    /p:Version=$Version /p:InformationalVersion=$Version
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# Package only the runtime files agents need to swap
New-Item -ItemType Directory -Force -Path $distDir | Out-Null
$zipPath = Join-Path $distDir "agent-update-$Version.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath }

$files = Get-ChildItem $outDir -File | Where-Object {
    $_.Extension -in '.exe', '.dll', '.json' -and $_.Name -ne 'appsettings.Development.json'
}
Compress-Archive -Path $files.FullName -DestinationPath $zipPath

$hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLower()
$size = (Get-Item $zipPath).Length

Write-Host ""
Write-Host "Package: $zipPath"
Write-Host "Size:    $size bytes"
Write-Host "SHA256:  $hash"
Write-Host ""
Write-Host "Upload (admin JWT required):"
Write-Host "  curl -X POST https://<server>/api/agent-updates \"
Write-Host "    -H `"Authorization: Bearer <token>`" \"
Write-Host "    -F `"file=@$zipPath`" -F `"version=$Version`" -F `"rolloutPercent=100`""
