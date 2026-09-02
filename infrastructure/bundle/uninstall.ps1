<#
.SYNOPSIS
    Removes the Gatus Kiosk client: restores the original shell, removes
    restrictions, stops/deletes the agent service, removes files.

.PARAMETER KioskUser
    The kiosk account that was locked down. Default: KioskUser

.PARAMETER KeepData
    Keep %ProgramData%\SentinelKiosk (logs, content, credentials).

.PARAMETER RemoveUser
    Also delete the local kiosk user account.

.EXAMPLE
    .\uninstall.ps1
    .\uninstall.ps1 -RemoveUser
#>
param(
    [string]$KioskUser = 'KioskUser',
    [switch]$KeepData,
    [switch]$RemoveUser
)

$ErrorActionPreference = 'Continue'
function Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { throw 'Run uninstall.ps1 as Administrator.' }

$programData = "$env:ProgramData\SentinelKiosk"
$backupDir = Join-Path $programData 'backup'

Step 'Restore shell'
try {
    $sid = (New-Object System.Security.Principal.NTAccount($KioskUser)).Translate([System.Security.Principal.SecurityIdentifier]).Value
    $userWinlogon = "Registry::HKEY_USERS\$sid\Software\Microsoft\Windows NT\CurrentVersion\Winlogon"
    if (Test-Path $userWinlogon) {
        Remove-ItemProperty -Path $userWinlogon -Name 'Shell' -ErrorAction SilentlyContinue
        Write-Host "  Removed per-user shell override for $KioskUser" -ForegroundColor Green
    }
    # Remove restrictions
    $policies = "Registry::HKEY_USERS\$sid\Software\Microsoft\Windows\CurrentVersion\Policies"
    Remove-ItemProperty -Path "$policies\System" -Name 'DisableTaskMgr' -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path "$policies\System" -Name 'DisableChangePassword' -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path "$policies\Explorer" -Name 'NoClose' -ErrorAction SilentlyContinue
    Write-Host '  Restrictions removed' -ForegroundColor Green
} catch { Write-Warning "Could not resolve/clean user hive for '$KioskUser': $($_.Exception.Message)" }

# If profile was never loaded, nothing to restore; remove first-logon task
Unregister-ScheduledTask -TaskName 'Gatus-KioskShell' -Confirm:$false -ErrorAction SilentlyContinue

# Default-shell backup restore is only needed if machine-wide shell was replaced
$shellBackup = Join-Path $backupDir 'winlogon-shell.txt'
if (Test-Path $shellBackup) {
    Write-Host "  Original shell backup preserved at $shellBackup (machine default was not changed by setup)" -ForegroundColor Yellow
}

Step 'Remove agent service'
Stop-Service 'SentinelKioskAgent' -Force -ErrorAction SilentlyContinue
sc.exe delete SentinelKioskAgent | Out-Null
Write-Host '  Service removed' -ForegroundColor Green

Step 'Remove files'
Remove-Item "$env:ProgramFiles\SentinelKiosk" -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "  Removed $env:ProgramFiles\SentinelKiosk" -ForegroundColor Green

if (-not $KeepData) {
    Remove-Item $programData -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  Removed $programData" -ForegroundColor Green
} else {
    Write-Host "  Kept $programData (-KeepData)" -ForegroundColor Yellow
}

if ($RemoveUser) {
    Remove-LocalUser -Name $KioskUser -ErrorAction SilentlyContinue
    Write-Host "  Removed local user '$KioskUser'" -ForegroundColor Green
}

Write-Host "`nUninstall complete. Reboot recommended." -ForegroundColor Cyan
