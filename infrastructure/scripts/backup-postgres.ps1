<#
.SYNOPSIS
    Backs up the Gatus Postgres database to a compressed custom-format dump,
    verifies the dump, and prunes backups older than the retention window.

.PARAMETER ContainerName
    Docker container running Postgres (default: kiosk-postgres for dev;
    use gatus-postgres for the production compose stack).

.PARAMETER OutDir
    Backup destination directory (created if missing). Default: <repo>\backups

.PARAMETER RetentionDays
    Delete *.dump files older than this many days. Default 30. 0 = keep forever.

.EXAMPLE
    .\backup-postgres.ps1
    .\backup-postgres.ps1 -ContainerName gatus-postgres -OutDir D:\backups -RetentionDays 60
#>
param(
    [string]$ContainerName = 'kiosk-postgres',
    [string]$Database = 'kiosk',
    [string]$User = 'kiosk',
    [string]$OutDir = '',
    [int]$RetentionDays = 30
)

$ErrorActionPreference = 'Stop'
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
if ([string]::IsNullOrEmpty($OutDir)) {
    $OutDir = Join-Path (Resolve-Path (Join-Path $scriptDir '..\..')) 'backups'
}
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$dumpFile = Join-Path $OutDir "kiosk-$timestamp.dump"

Write-Host "[backup] Dumping $Database from $ContainerName -> $dumpFile"

# -Fc = custom format (compressed). Dump INSIDE the container then docker cp out --
# piping binary through PowerShell redirection would corrupt it (PS 5.1 re-encodes).
$containerTmp = "/tmp/kiosk-$timestamp.dump"
docker exec $ContainerName pg_dump -U $User -d $Database -Fc -f $containerTmp
if ($LASTEXITCODE -ne 0) { throw "pg_dump failed (exit $LASTEXITCODE)" }

docker cp "${ContainerName}:$containerTmp" $dumpFile
if ($LASTEXITCODE -ne 0) { docker exec $ContainerName rm -f $containerTmp | Out-Null; throw "docker cp failed" }

# Integrity check: list the dump's TOC before deleting the container copy
docker exec $ContainerName pg_restore --list $containerTmp | Out-Null
$verifyExit = $LASTEXITCODE
docker exec $ContainerName rm -f $containerTmp | Out-Null
if ($verifyExit -ne 0) { Remove-Item $dumpFile; throw "Dump failed pg_restore --list integrity check" }

$size = (Get-Item $dumpFile).Length
if ($size -lt 100) { Remove-Item $dumpFile; throw "Dump suspiciously small ($size bytes) -- refusing to keep" }

Write-Host "[backup] OK: $([math]::Round($size/1KB, 1)) KB, integrity verified"

# Retention prune
if ($RetentionDays -gt 0) {
    $cutoff = (Get-Date).AddDays(-$RetentionDays)
    $pruned = 0
    Get-ChildItem $OutDir -Filter 'kiosk-*.dump' | Where-Object { $_.LastWriteTime -lt $cutoff } | ForEach-Object {
        Remove-Item $_.FullName -Force; $pruned++
    }
    if ($pruned -gt 0) { Write-Host "[backup] Pruned $pruned backup(s) older than $RetentionDays days" }
}

Write-Host "[backup] Done: $dumpFile"
