<#
.SYNOPSIS
    Registers a daily Windows Task Scheduler job that runs the Postgres + AppData
    backups. Dev-box convenience; on a Linux prod host, schedule the same scripts
    via cron instead (see docs/BACKUP-RESTORE.md).

.EXAMPLE
    # Run as Administrator
    .\register-backup-task.ps1 -At 02:30
    .\register-backup-task.ps1 -At 02:30 -ContainerName gatus-postgres -AppDataMode volume
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$At,                       # e.g. "02:30"
    [string]$TaskName = 'Gatus-Backup',
    [string]$ContainerName = 'kiosk-postgres',
    [ValidateSet('local', 'volume')]
    [string]$AppDataMode = 'local',
    [string]$VolumeName = 'gatus_api-content',
    [int]$RetentionDays = 30
)

$ErrorActionPreference = 'Stop'
$scriptsDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$logDir = Join-Path (Resolve-Path (Join-Path $scriptsDir '..\..')) 'backups'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

$wrapper = Join-Path $scriptsDir 'run-backup.ps1'
@"
`$log = Join-Path '$logDir' "backup-`$(Get-Date -Format 'yyyyMMdd').log"
Start-Transcript -Path `$log -Append | Out-Null
& '$scriptsDir\backup-postgres.ps1' -ContainerName '$ContainerName' -RetentionDays $RetentionDays
& '$scriptsDir\backup-appdata.ps1' -Mode $AppDataMode -VolumeName '$VolumeName' -RetentionDays $RetentionDays
Stop-Transcript | Out-Null
"@ | Set-Content $wrapper -Encoding UTF8

$action = New-ScheduledTaskAction -Execute 'powershell.exe' `
    -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$wrapper`""
$trigger = New-ScheduledTaskTrigger -Daily -At $At
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -DontStopOnIdleEnd

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger `
    -Settings $settings -RunLevel Highest -Force | Out-Null

Write-Host "Registered scheduled task '$TaskName' daily at $At"
Write-Host "Wrapper script: $wrapper"
Write-Host "Logs: $logDir\backup-YYYYMMDD.log"
Write-Host "Test now with:  Start-ScheduledTask -TaskName '$TaskName'"
