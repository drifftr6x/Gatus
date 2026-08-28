#Requires -RunAsAdministrator
# Sentinel Kiosk Agent Uninstaller

param(
    [string]$ServiceName = "SentinelKioskAgent",
    [string]$InstallPath = "$env:ProgramFiles\SentinelKiosk",
    [switch]$RemoveData
)

$ErrorActionPreference = "Stop"

Write-Host "=== Sentinel Kiosk Agent Uninstaller ===" -ForegroundColor Cyan
Write-Host ""

# Stop service
Write-Host "Stopping service..." -ForegroundColor Yellow
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -eq "Running") {
        Stop-Service -Name $ServiceName -Force
        Write-Host "  Service stopped" -ForegroundColor Green
    }

    # Remove service
    sc.exe delete $ServiceName | Out-Null
    Write-Host "  Service removed" -ForegroundColor Green
} else {
    Write-Host "  Service not found" -ForegroundColor Yellow
}

# Remove installation directory
Write-Host "Removing installation files..." -ForegroundColor Yellow
if (Test-Path $InstallPath) {
    Remove-Item -Path $InstallPath -Recurse -Force
    Write-Host "  Removed: $InstallPath" -ForegroundColor Green
} else {
    Write-Host "  Install path not found" -ForegroundColor Yellow
}

# Remove data directory (optional)
if ($RemoveData) {
    Write-Host "Removing data directory..." -ForegroundColor Yellow
    $programDataPath = "$env:ProgramData\SentinelKiosk"
    if (Test-Path $programDataPath) {
        Remove-Item -Path $programDataPath -Recurse -Force
        Write-Host "  Removed: $programDataPath" -ForegroundColor Green
    }
} else {
    Write-Host "Data directory preserved (use -RemoveData to delete)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Uninstall Complete ===" -ForegroundColor Cyan
