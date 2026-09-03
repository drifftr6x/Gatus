<#
.SYNOPSIS
    Gatus Kiosk client setup -- Windows 11 Pro, domain-joined.
    Installs the management agent (service) and kiosk runtime, enrolls the device,
    and applies the Pro-appropriate lockdown (per-user Winlogon shell replacement
    + registry restrictions) for the kiosk user.

    Run as Administrator. Reboot (or sign in as the kiosk user) to enter kiosk mode.

.PARAMETER ServerUrl
    Public API origin, e.g. https://gatus.contoso.com
    Optional when the bundle has server-config.json (auto-detected at build time).

.PARAMETER EnrollmentToken
    One-time token from the admin console (Devices -> Enroll a Device).
    Optional when baked into the bundle via build-client-bundle.ps1 -EnrollmentToken.

.PARAMETER KioskUser
    Local account the kiosk session runs under (created if missing). Default: KioskUser

.PARAMETER UseDomainUser
    Instead of creating a local user, lock down an existing DOMAIN\user.

.PARAMETER WhatIf
    Dry run -- prints every action without changing the system.

.EXAMPLE
    .\setup.ps1 -EnrollmentToken abc123                    # server auto-detected from bundle
    .\setup.ps1                                            # zero-touch (token + server embedded)
    .\setup.ps1 -ServerUrl https://gatus.contoso.com -EnrollmentToken abc123  # explicit
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $false)]
    [string]$ServerUrl,
    [Parameter(Mandatory = $false)]
    [string]$EnrollmentToken,
    [string]$KioskUser = 'KioskUser',
    [string]$UseDomainUser = '',
    [switch]$AllowPowerButton
)

$ErrorActionPreference = 'Stop'
$dryRun = -not $PSCmdlet.ShouldProcess('this computer', 'Gatus Kiosk setup')

function Test-PendingReboot {
    return (Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending') -or
           (Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired')
}

function Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Ok($msg) { Write-Host "  $msg" -ForegroundColor Green }
function Warn($msg) { Write-Host "  $msg" -ForegroundColor Yellow }

# -- Prechecks ---------------------------------------------------------------
Step 'Preflight checks'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin -and -not $dryRun) { throw 'Run setup.ps1 as Administrator.' }
if ($dryRun) { Warn 'Dry run: no changes will be made.' }
Ok 'Running as Administrator'

# -- Auto-detect server URL + enrollment token from bundle config ------------
$configPath = Join-Path $PSScriptRoot 'server-config.json'
$bundleConfig = $null
if (Test-Path $configPath) {
    $bundleConfig = Get-Content $configPath -Raw | ConvertFrom-Json
}

if (-not $ServerUrl) {
    if ($bundleConfig -and $bundleConfig.serverUrl) {
        $ServerUrl = $bundleConfig.serverUrl
        Ok "Server URL from bundle config: $ServerUrl"
    }
    else {
        throw 'No -ServerUrl provided and no server-config.json found in bundle. Rebuild the bundle or pass -ServerUrl explicitly.'
    }
}
else {
    Ok "Server URL: $ServerUrl (from parameter)"
}

if (-not $EnrollmentToken) {
    if ($bundleConfig -and $bundleConfig.enrollmentToken) {
        $EnrollmentToken = $bundleConfig.enrollmentToken
        Ok 'Enrollment token from bundle config (single-use)'
    }
    else {
        throw 'No -EnrollmentToken provided and none baked into bundle. Pass -EnrollmentToken or rebuild with: build-client-bundle.ps1 -EnrollmentToken <token>'
    }
}

$os = Get-CimInstance Win32_OperatingSystem
$caption = $os.Caption
Ok "OS: $caption (build $($os.BuildNumber))"
if ($caption -notmatch 'Windows 1[01]') { Warn "Untested OS version: $caption" }
if ($caption -match 'Enterprise|Education') {
    Warn 'Enterprise/Education detected -- this bundle targets Pro (Winlogon shell). Shell Launcher would be preferable; setup continues with the Pro path.'
}

$cs = Get-CimInstance Win32_ComputerSystem
if ($cs.PartOfDomain) { Ok "Domain-joined: $($cs.Domain)" } else { Warn 'Not domain-joined -- setup works, but GPO lockdown conflicts are not a concern here.' }

if (Test-PendingReboot) { Warn 'A reboot is pending -- consider rebooting before install.' }

# -- Paths -------------------------------------------------------------------
$agentInstall = "$env:ProgramFiles\SentinelKiosk\Agent"
$runtimeInstall = "$env:ProgramFiles\SentinelKiosk\Runtime"
$programData = "$env:ProgramData\SentinelKiosk"
$backupDir = Join-Path $programData 'backup'
$bundleDir = $PSScriptRoot

# -- Kiosk user --------------------------------------------------------------
Step 'Kiosk user account'
$targetUser = ''
$targetUserSid = ''

if ($UseDomainUser) {
    $targetUser = $UseDomainUser
    Ok "Will lock down domain user: $targetUser"
} else {
    $existing = Get-LocalUser -Name $KioskUser -ErrorAction SilentlyContinue
    if (-not $existing) {
        if (-not $dryRun) {
            $pw = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 24 | ForEach-Object { [char]$_ }) + '!1'
            $secure = ConvertTo-SecureString $pw -AsPlainText -Force
            New-LocalUser -Name $KioskUser -Password $secure -PasswordNeverExpires -UserMayNotChangePassword -FullName 'Gatus Kiosk' | Out-Null
            Ok "Created local user '$KioskUser' (random password, never expires)"
        } else { Ok "[dry-run] would create local user '$KioskUser'" }
    } else {
        Ok "Local user '$KioskUser' already exists"
    }
    $targetUser = $KioskUser
}

# -- Agent -------------------------------------------------------------------
Step 'Install management agent'
if (-not $dryRun) {
    New-Item -ItemType Directory -Force -Path $agentInstall, "$programData\Config", $backupDir | Out-Null
    Copy-Item (Join-Path $bundleDir 'agent\*') $agentInstall -Recurse -Force
    Ok "Agent files -> $agentInstall"

    # Agent production config (server URL + intervals)
    @{
        Agent = @{
            ServerUrl = $ServerUrl
            HeartbeatIntervalSeconds = 30
            PolicySyncIntervalSeconds = 300
            DeploymentPollIntervalSeconds = 60
            CommandPollIntervalSeconds = 15
            TelemetryUploadIntervalSeconds = 300
            UpdateCheckIntervalSeconds = 3600
        }
    } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $agentInstall 'appsettings.Production.json') -Encoding UTF8
    Ok "Agent config written (ServerUrl=$ServerUrl)"

    # Enrollment token for first start (agent deletes it after use)
    $EnrollmentToken | Set-Content (Join-Path $programData 'Config\enrollment-token.txt') -Encoding UTF8
    Ok 'Enrollment token staged'

    $svc = Get-Service -Name 'SentinelKioskAgent' -ErrorAction SilentlyContinue
    if ($svc) {
        Stop-Service 'SentinelKioskAgent' -Force -ErrorAction SilentlyContinue
        sc.exe delete SentinelKioskAgent | Out-Null
        Start-Sleep -Seconds 2
    }
    $exe = Join-Path $agentInstall 'SentinelKiosk.Agent.exe'
    New-Service -Name 'SentinelKioskAgent' -BinaryPathName "`"$exe`"" `
        -DisplayName 'Sentinel Kiosk Agent' -StartupType Automatic `
        -Description 'Gatus kiosk management agent' | Out-Null
    # Recovery: restart on failure
    sc.exe failure SentinelKioskAgent reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null
    sc.exe config SentinelKioskAgent start= delayed-auto | Out-Null
    Start-Service 'SentinelKioskAgent'
    Ok 'Service SentinelKioskAgent installed and started (recovery: restart on failure)'
} else {
    Ok "[dry-run] would copy agent to $agentInstall, write config, stage token, install+start service"
}

# -- Runtime -----------------------------------------------------------------
Step 'Install kiosk runtime'
if (-not $dryRun) {
    New-Item -ItemType Directory -Force -Path $runtimeInstall | Out-Null
    Copy-Item (Join-Path $bundleDir 'runtime\*') $runtimeInstall -Recurse -Force
    Ok "Runtime files -> $runtimeInstall"
} else { Ok "[dry-run] would copy runtime to $runtimeInstall" }

$runtimeExe = Join-Path $runtimeInstall 'SentinelKiosk.Runtime.exe'

# -- Shell replacement (per-user Winlogon shell) -----------------------------
Step "Lock down shell for '$targetUser' (Windows 11 Pro path)"

# Resolve SID (local or domain)
if (-not $dryRun) {
    try {
        $acct = New-Object System.Security.Principal.NTAccount($targetUser)
        $targetUserSid = $acct.Translate([System.Security.Principal.SecurityIdentifier]).Value
        Ok "Resolved SID: $targetUserSid"
    } catch { throw "Cannot resolve user '$targetUser': $($_.Exception.Message)" }

    $shellKey = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"
    $origShell = (Get-ItemProperty $shellKey -Name Shell -ErrorAction SilentlyContinue).Shell
    if ($origShell) {
        $origShell | Set-Content (Join-Path $backupDir 'winlogon-shell.txt')
        Ok "Backed up default shell: $origShell"
    }

    # Per-user shell override lives under the user's hive via HKU after first logon.
    # Reliable approach pre-first-logon: set the machine DEFAULT shell only when this
    # machine is kiosk-dedicated, else instruct operator. We use the documented
    # per-user mechanism: HKLM Winlogon 'Shell' applies to all users, so instead we
    # use Assigned-Access-style per-user shell via 'HKU\<SID>\...\Winlogon' once the
    # profile exists; until then, register a first-logon hook.
    $userWinlogon = "Registry::HKEY_USERS\$targetUserSid\Software\Microsoft\Windows NT\CurrentVersion\Winlogon"
    $profileLoaded = Test-Path "Registry::HKEY_USERS\$targetUserSid"

    if ($profileLoaded) {
        New-Item -Path $userWinlogon -Force | Out-Null
        Set-ItemProperty -Path $userWinlogon -Name 'Shell' -Value $runtimeExe
        Ok "Per-user shell set: $runtimeExe"
    } else {
        # Profile not loaded (never logged in). Queue a scheduled task at first logon.
        Warn 'Kiosk profile not loaded yet (user has never signed in). Registering first-logon shell task.'
        $taskScript = @"
New-Item -Path 'Registry::HKEY_USERS\$targetUserSid\Software\Microsoft\Windows NT\CurrentVersion\Winlogon' -Force | Out-Null
Set-ItemProperty -Path 'Registry::HKEY_USERS\$targetUserSid\Software\Microsoft\Windows NT\CurrentVersion\Winlogon' -Name 'Shell' -Value '$runtimeExe'
"@
        $taskScript | Set-Content (Join-Path $programData 'set-kiosk-shell.ps1') -Encoding UTF8
        $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$programData\set-kiosk-shell.ps1`""
        $trigger = New-ScheduledTaskTrigger -AtLogOn -User $targetUser
        Register-ScheduledTask -TaskName 'Gatus-KioskShell' -Action $action -Trigger $trigger -RunLevel Highest -Force | Out-Null
        Ok 'First-logon task Gatus-KioskShell registered'
    }

    # -- Base restrictions for the kiosk user -------------------------------
    Step 'Registry restrictions'
    $policies = "Registry::HKEY_USERS\$targetUserSid\Software\Microsoft\Windows\CurrentVersion\Policies"
    if ($profileLoaded) {
        New-Item -Path "$policies\System" -Force | Out-Null
        Set-ItemProperty -Path "$policies\System" -Name 'DisableTaskMgr' -Value 1 -Type DWord
        Set-ItemProperty -Path "$policies\System" -Name 'DisableChangePassword' -Value 1 -Type DWord
        if (-not $AllowPowerButton) {
            New-Item -Path "$policies\Explorer" -Force | Out-Null
            Set-ItemProperty -Path "$policies\Explorer" -Name 'NoClose' -Value 1 -Type DWord
        }
        Ok 'Restrictions applied (TaskMgr, change-password, power)'
    } else {
        Warn 'Restrictions deferred: applied by first-logon task (profile not loaded).'
        Add-Content (Join-Path $programData 'set-kiosk-shell.ps1') "`nNew-Item -Path 'Registry::HKEY_USERS\$targetUserSid\Software\Microsoft\Windows\CurrentVersion\Policies\System' -Force | Out-Null"
        Add-Content (Join-Path $programData 'set-kiosk-shell.ps1') "`nSet-ItemProperty -Path 'Registry::HKEY_USERS\$targetUserSid\Software\Microsoft\Windows\CurrentVersion\Policies\System' -Name 'DisableTaskMgr' -Value 1 -Type DWord"
    }
} else {
    Ok "[dry-run] would back up shell, set per-user Winlogon shell to runtime exe, apply registry restrictions"
}

# -- Summary -----------------------------------------------------------------
Step 'Setup complete'
Write-Host @"

Next steps:
  1. In the admin console: verify '$env:COMPUTERNAME' appears Online within ~60s
     ($ServerUrl -> Devices)
  2. Assign the kiosk policy to this device (home URL, allowlists, lockdown profile)
  3. Push content (Content -> Deploy)
  4. Sign in as '$targetUser' to enter kiosk mode (or reboot on kiosk-dedicated machines)

Recovery:
  - Maintenance/restore: .\uninstall.ps1 (restores original shell, removes service)
  - Agent logs: $programData\Logs\
  - Shell backup: $backupDir\winlogon-shell.txt

"@ -ForegroundColor White
