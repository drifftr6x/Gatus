#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs Sentinel Kiosk Agent + Runtime on a store PC.

.EXAMPLE
    .\install-kiosk.ps1 -ServerUrl "http://10.2.23.121:5163" -EnrollmentToken "abc..." -KioskLockdown
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$ServerUrl,

    [Parameter(Mandatory=$true)]
    [string]$EnrollmentToken,

    [string]$InstallPath = "$env:ProgramFiles\SentinelKiosk",
    [switch]$KioskLockdown,
    [switch]$ReplaceShell
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$agentSrc = Join-Path $root "windows-agent\bin\Debug\net10.0-windows\win-x64"
$runtimeSrc = Join-Path $root "windows-kiosk-runtime\bin\Debug\net10.0-windows"

if (-not (Test-Path (Join-Path $agentSrc "SentinelKiosk.Agent.exe"))) {
    throw "Agent exe not found at $agentSrc — build the agent first."
}
if (-not (Test-Path (Join-Path $runtimeSrc "SentinelKiosk.Runtime.exe"))) {
    throw "Runtime exe not found at $runtimeSrc — build the kiosk runtime first."
}

Write-Host "=== Sentinel Kiosk installer ===" -ForegroundColor Cyan
New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
$programData = "$env:ProgramData\SentinelKiosk"
foreach ($dir in @("Config", "Content", "Logs", "Cache", "State", "Updates")) {
    New-Item -ItemType Directory -Path (Join-Path $programData $dir) -Force | Out-Null
}

Copy-Item "$agentSrc\SentinelKiosk.Agent.exe" $InstallPath -Force
Copy-Item "$agentSrc\appsettings.json" $InstallPath -Force -ErrorAction SilentlyContinue
Copy-Item "$runtimeSrc\SentinelKiosk.Runtime.exe" $InstallPath -Force
Get-ChildItem $runtimeSrc -Filter "WebView2Loader.dll" -ErrorAction SilentlyContinue | Copy-Item -Destination $InstallPath -Force
Get-ChildItem $runtimeSrc -Filter "Microsoft.Web.WebView2*.dll" -ErrorAction SilentlyContinue | Copy-Item -Destination $InstallPath -Force

@{
    Agent = @{
        ServerUrl = $ServerUrl
        HeartbeatIntervalSeconds = 30
        PolicySyncIntervalSeconds = 60
        DeploymentCheckIntervalSeconds = 30
        CommandPollIntervalSeconds = 15
        TelemetryBatchSize = 100
        TelemetryUploadIntervalSeconds = 300
    }
} | ConvertTo-Json -Depth 10 | Out-File "$InstallPath\appsettings.json" -Encoding UTF8

$EnrollmentToken | Out-File "$programData\Config\enrollment-token.txt" -Encoding UTF8

$svc = "SentinelKioskAgent"
$existing = Get-Service -Name $svc -ErrorAction SilentlyContinue
if ($existing) {
    Stop-Service $svc -Force -ErrorAction SilentlyContinue
    sc.exe delete $svc | Out-Null
    Start-Sleep 2
}

New-Service -Name $svc `
    -BinaryPathName "`"$InstallPath\SentinelKiosk.Agent.exe`"" `
    -DisplayName "Sentinel Kiosk Agent" `
    -StartupType Automatic | Out-Null
sc.exe failure $svc reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

$runtimeExe = Join-Path $InstallPath "SentinelKiosk.Runtime.exe"
schtasks.exe /Create /TN "SentinelKioskRuntime" /TR "`"$runtimeExe`"" /SC ONLOGON /RL LIMITED /F | Out-Null
Write-Host "  Logon task created for kiosk runtime" -ForegroundColor Green

if ($ReplaceShell) {
    Write-Host "  WARNING: replacing Winlogon Shell (machine-wide)" -ForegroundColor Yellow
    $regPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"
    $original = (Get-ItemProperty -Path $regPath -Name "Shell" -ErrorAction SilentlyContinue).Shell
    if ($original -and $original -ne $runtimeExe) {
        Set-ItemProperty -Path $regPath -Name "ShellBackup" -Value $original
    }
    Set-ItemProperty -Path $regPath -Name "Shell" -Value $runtimeExe
}

Start-Service $svc
Start-Process -FilePath (Join-Path $InstallPath "SentinelKiosk.Agent.exe") -ArgumentList "--enroll",$EnrollmentToken -Wait -WindowStyle Hidden -ErrorAction SilentlyContinue
# Agent service already starts; enrollment token file is consumed on first run if EnrollmentService supports it.

Write-Host ""
Write-Host "Installed to $InstallPath" -ForegroundColor Green
Write-Host "Server: $ServerUrl" -ForegroundColor White
if ($KioskLockdown) {
    Write-Host "Enable kiosk lockdown from the admin console (device detail → Kiosk mode)." -ForegroundColor Yellow
}
Write-Host "Start the runtime now with: schtasks /Run /TN SentinelKioskRuntime" -ForegroundColor White
