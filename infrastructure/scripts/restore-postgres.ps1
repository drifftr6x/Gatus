<#
.SYNOPSIS
    Restores a backup dump into the Gatus Postgres database.
    Takes a pre-restore snapshot of the CURRENT database first, so a bad
    restore is itself recoverable.

.PARAMETER DumpFile
    Path to a kiosk-*.dump file produced by backup-postgres.ps1.

.PARAMETER Force
    Required when the target database already contains tables.

.EXAMPLE
    .\restore-postgres.ps1 -DumpFile ..\..\backups\kiosk-20260908-120000.dump -Force
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$DumpFile,
    [string]$ContainerName = 'kiosk-postgres',
    [string]$Database = 'kiosk',
    [string]$User = 'kiosk',
    [string]$SnapshotDir = '',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
if ([string]::IsNullOrEmpty($SnapshotDir)) {
    $SnapshotDir = Join-Path (Resolve-Path (Join-Path $scriptDir '..\..')) 'backups'
}
New-Item -ItemType Directory -Force -Path $SnapshotDir | Out-Null
if (-not (Test-Path $DumpFile)) { throw "Dump file not found: $DumpFile" }
$DumpFile = Resolve-Path $DumpFile

# Refuse to clobber an existing database without -Force
$tableCount = docker exec $ContainerName psql -U $User -d $Database -t -A -c `
    "SELECT count(*) FROM information_schema.tables WHERE table_schema='public';"
if ($LASTEXITCODE -ne 0) { throw "Cannot reach database $Database on $ContainerName" }

if ([int]$tableCount -gt 0 -and -not $Force) {
    throw "Database '$Database' already has $tableCount tables. Re-run with -Force to overwrite (a pre-restore snapshot will be taken first)."
}

# Pre-restore snapshot of current state (dump inside container, cp out -- binary-safe)
if ([int]$tableCount -gt 0) {
    $snapshot = Join-Path $SnapshotDir "pre-restore-$(Get-Date -Format 'yyyyMMdd-HHmmss').dump"
    Write-Host "[restore] Taking pre-restore snapshot -> $snapshot"
    $snapTmp = "/tmp/pre-restore-$(Get-Date -Format 'yyyyMMddHHmmss').dump"
    docker exec $ContainerName pg_dump -U $User -d $Database -Fc -f $snapTmp
    if ($LASTEXITCODE -ne 0) { throw "Pre-restore snapshot failed -- aborting restore" }
    docker cp "${ContainerName}:$snapTmp" $snapshot
    docker exec $ContainerName rm -f $snapTmp | Out-Null
}

# Copy dump into container and restore
$containerTmp = "/tmp/restore-$(Get-Date -Format 'yyyyMMddHHmmss').dump"
Write-Host "[restore] Uploading $DumpFile ..."
docker cp $DumpFile "${ContainerName}:$containerTmp"
if ($LASTEXITCODE -ne 0) { throw "docker cp into container failed" }

Write-Host "[restore] Restoring into $Database (--clean --if-exists)..."
docker exec $ContainerName pg_restore -U $User -d $Database --clean --if-exists --no-owner $containerTmp
$restoreExit = $LASTEXITCODE
docker exec $ContainerName rm -f $containerTmp | Out-Null
# pg_restore exits non-zero on benign warnings too; report but verify below
if ($restoreExit -ne 0) { Write-Warning "pg_restore exited $restoreExit (may include benign warnings) -- verifying database state" }

# Post-restore verification
$newTableCount = docker exec $ContainerName psql -U $User -d $Database -t -A -c `
    "SELECT count(*) FROM information_schema.tables WHERE table_schema='public';"
$migrationSql = 'SELECT max("MigrationId") FROM "__EFMigrationsHistory";'
$migration = $migrationSql | docker exec -i $ContainerName psql -U $User -d $Database -t -A 2>$null

Write-Host "[restore] Tables: $newTableCount | Latest migration: $migration"
if ([int]$newTableCount -lt 5) { throw "Restore verification failed -- only $newTableCount tables present" }

Write-Host "[restore] Done."
