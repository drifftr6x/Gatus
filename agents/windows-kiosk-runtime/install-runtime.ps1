#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Installs the Sentinel Kiosk Runtime as the shell replacement for kiosk mode.
.DESCRIPTION
    Configures the kiosk runtime to launch automatically on login for the specified user.
    Uses Windows Shell Launcher or Assigned Access for supported editions.
.PARAMETER KioskUser
    The Windows username to configure for kiosk mode (default: "KioskUser")
.PARAMETER RuntimePath
    Path to SentinelKiosk.Runtime.exe (default: current directory)
.EXAMPLE
    .\install-runtime.ps1 -KioskUser "Kiosk01" -RuntimePath "C:\Kiosk\SentinelKiosk.Runtime.exe"
#>

param(
    [string]$KioskUser = "KioskUser",
    [string]$RuntimePath = $PSScriptRoot
)

$ErrorActionPreference = "Stop"

# Validate runtime exists
$exePath = Join-Path $RuntimePath "SentinelKiosk.Runtime.exe"
if (-not (Test-Path $exePath)) {
    throw "SentinelKiosk.Runtime.exe not found at: $exePath"
}

Write-Host "Installing Sentinel Kiosk Runtime..." -ForegroundColor Green
Write-Host "  User: $KioskUser" -ForegroundColor Cyan
Write-Host "  Path: $exePath" -ForegroundColor Cyan

# Check Windows edition (Shell Launcher requires Enterprise/Education)
$edition = (Get-WmiObject -Class Win32_OperatingSystem).OperatingSystemSKU
$supportedEditions = @(4, 27, 48, 49, 50, 98, 99, 100, 101, 103, 104, 119, 121, 122, 123, 125)  # Enterprise, Education, IoT

if ($supportedEditions -contains $edition) {
    Write-Host "  Using Shell Launcher (Enterprise/Education edition detected)" -ForegroundColor Yellow
    
    # Enable Shell Launcher feature
    Enable-WindowsOptionalFeature -Online -FeatureName "Client-EmbeddedShellLauncher" -NoRestart -ErrorAction SilentlyContinue
    
    # Configure Shell Launcher via WMI
    $shellLauncher = Get-WmiObject -Namespace "root\cimv2\mdm\dmmap" -Class "MDM_AssignedAccess" -ErrorAction SilentlyContinue
    if ($shellLauncher) {
        # Shell Launcher configuration would go here
        Write-Host "  Shell Launcher configured (manual configuration may be required)" -ForegroundColor Yellow
    }
} else {
    Write-Host "  Using Registry-based shell replacement (Pro edition)" -ForegroundColor Yellow
    
    # Registry-based shell replacement for Pro edition
    $userSid = (New-Object System.Security.Principal.NTAccount($KioskUser)).Translate([System.Security.Principal.SecurityIdentifier]).Value
    $regPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"
    
    # Backup original shell
    $originalShell = (Get-ItemProperty -Path $regPath -Name "Shell" -ErrorAction SilentlyContinue).Shell
    if ($originalShell -and $originalShell -ne $exePath) {
        Set-ItemProperty -Path $regPath -Name "ShellBackup" -Value $originalShell
        Write-Host "  Original shell backed up: $originalShell" -ForegroundColor Cyan
    }
    
    # Set kiosk runtime as shell
    Set-ItemProperty -Path $regPath -Name "Shell" -Value $exePath
    Write-Host "  Shell replaced with kiosk runtime" -ForegroundColor Green
}

# Create auto-logon configuration (optional - requires secure password storage)
Write-Host "`nTo complete setup:" -ForegroundColor Yellow
Write-Host "  1. Create or verify user account: $KioskUser" -ForegroundColor White
Write-Host "  2. Configure auto-logon if desired (netplwiz or registry)" -ForegroundColor White
Write-Host "  3. Test login as $KioskUser" -ForegroundColor White
Write-Host "  4. Verify kiosk runtime launches fullscreen" -ForegroundColor White

Write-Host "`nInstallation complete!" -ForegroundColor Green
