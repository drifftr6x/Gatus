<#
.SYNOPSIS
    Backs up the API AppData directory: content packages, agent update packages,
    and the RSA signing keys. A DB backup without these leaves deployments
    pointing at missing packages -- and losing the signing private key means no
    agent will ever verify a new package again.

    Dev:    reads the local apps\api-server\AppData folder.
    Prod:   reads the compose 'api-content' named volume via a throwaway container.

.PARAMETER Mode
    'local' (dev folder) or 'volume' (docker named volume). Default: local.

.EXAMPLE
    .\backup-appdata.ps1                                  # dev
    .\backup-appdata.ps1 -Mode volume -VolumeName gatus_api-content   # prod
#>
param(
    [ValidateSet('local', 'volume')]
    [string]$Mode = 'local',
    [string]$LocalPath = '',
    [string]$VolumeName = 'gatus_api-content',
    [string]$OutDir = '',
    [int]$RetentionDays = 30
)

$ErrorActionPreference = 'Stop'
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$repoRoot = Resolve-Path (Join-Path $scriptDir '..\..')
if ([string]::IsNullOrEmpty($LocalPath)) { $LocalPath = Join-Path $repoRoot 'apps\api-server\AppData' }
if ([string]::IsNullOrEmpty($OutDir)) { $OutDir = Join-Path $repoRoot 'backups' }
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$zipFile = Join-Path $OutDir "appdata-$timestamp.zip"

if ($Mode -eq 'local') {
    if (-not (Test-Path $LocalPath)) { throw "AppData not found at $LocalPath" }
    Write-Host "[appdata] Zipping $LocalPath -> $zipFile"
    Compress-Archive -Path (Join-Path $LocalPath '*') -DestinationPath $zipFile -CompressionLevel Optimal
}
else {
    Write-Host "[appdata] Exporting docker volume $VolumeName -> $zipFile"
    $hostTmp = Join-Path $env:TEMP "appdata-backup-$timestamp"
    New-Item -ItemType Directory -Force -Path $hostTmp | Out-Null
    try {
        # Copy volume contents out via a throwaway container, then zip on host
        docker run --rm -v "${VolumeName}:/data:ro" -v "${hostTmp}:/out" alpine sh -c "cd /data && tar cf /out/appdata.tar ."
        if ($LASTEXITCODE -ne 0) { throw "docker volume export failed" }
        Compress-Archive -Path (Join-Path $hostTmp 'appdata.tar') -DestinationPath $zipFile
    }
    finally {
        Remove-Item $hostTmp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$size = (Get-Item $zipFile).Length
if ($size -lt 50) { Remove-Item $zipFile; throw "AppData backup suspiciously small -- refusing to keep" }
Write-Host "[appdata] OK: $([math]::Round($size/1KB, 1)) KB"

# Sanity: signing key must be present (AppData\content\keys\signing.key)
if ($Mode -eq 'local') {
    $keyFile = Join-Path $LocalPath 'content\keys\signing.key'
    if (-not (Test-Path $keyFile)) {
        Write-Warning "signing.key not found under AppData\content\keys -- agents cannot verify packages after restore without it!"
    }
}

if ($RetentionDays -gt 0) {
    $cutoff = (Get-Date).AddDays(-$RetentionDays)
    Get-ChildItem $OutDir -Filter 'appdata-*.zip' | Where-Object { $_.LastWriteTime -lt $cutoff } | ForEach-Object {
        Remove-Item $_.FullName -Force
        Write-Host "[appdata] Pruned $($_.Name)"
    }
}

Write-Host "[appdata] Done: $zipFile"
