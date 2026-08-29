#Requires -RunAsAdministrator
# Sentinel Kiosk Agent Installer
# Run: .\install.ps1 -ServerUrl "https://kiosk-server.example.com" -EnrollmentToken "token123"

param(
    [Parameter(Mandatory=$true)]
    [string]$ServerUrl,

    [Parameter(Mandatory=$true)]
    [string]$EnrollmentToken,

    [string]$InstallPath = "$env:ProgramFiles\SentinelKiosk",
    [string]$ServiceName = "SentinelKioskAgent",
    [string]$ServiceDisplayName = "Sentinel Kiosk Agent",
    [string]$ServiceDescription = "Sentinel Kiosk management agent for Windows"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Sentinel Kiosk Agent Installer ===" -ForegroundColor Cyan
Write-Host ""

# Self-contained single-file publish — no .NET runtime needed
Write-Host "Using self-contained agent (no .NET runtime required)" -ForegroundColor Green

# Create install directory
Write-Host "Creating installation directory..." -ForegroundColor Yellow
if (-not (Test-Path $InstallPath)) {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}
Write-Host "  Path: $InstallPath" -ForegroundColor Green

# Create ProgramData directory
$programDataPath = "$env:ProgramData\SentinelKiosk"
Write-Host "Creating data directory..." -ForegroundColor Yellow
foreach ($dir in @("Config", "Content", "Logs", "Cache", "State", "Updates")) {
    $path = Join-Path $programDataPath $dir
    if (-not (Test-Path $path)) {
        New-Item -ItemType Directory -Path $path -Force | Out-Null
    }
}
Write-Host "  Path: $programDataPath" -ForegroundColor Green

# Copy agent files (assumes running from publish directory)
Write-Host "Copying agent files..." -ForegroundColor Yellow
$sourcePath = $PSScriptRoot
Copy-Item -Path "$sourcePath\SentinelKiosk.Agent.exe" -Destination $InstallPath -Force
Copy-Item -Path "$sourcePath\appsettings.json" -Destination $InstallPath -Force
Write-Host "  Copied to $InstallPath" -ForegroundColor Green

# Create agent configuration
Write-Host "Creating agent configuration..." -ForegroundColor Yellow
$agentConfig = @{
    Agent = @{
        ServerUrl = $ServerUrl
        HeartbeatIntervalSeconds = 30
        PolicySyncIntervalSeconds = 300
        DeploymentCheckIntervalSeconds = 60
        CommandPollIntervalSeconds = 15
        TelemetryBatchSize = 100
        TelemetryUploadIntervalSeconds = 300
    }
} | ConvertTo-Json -Depth 10

$agentConfig | Out-File -FilePath "$InstallPath\appsettings.Production.json" -Encoding UTF8
Write-Host "  Config saved" -ForegroundColor Green

# Create enrollment token file (one-time use)
$enrollmentFile = Join-Path $programDataPath "Config\enrollment-token.txt"
$EnrollmentToken | Out-File -FilePath $enrollmentFile -Encoding UTF8
Write-Host "  Enrollment token saved (will be deleted after first use)" -ForegroundColor Green

# Install Windows Service
Write-Host "Installing Windows Service..." -ForegroundColor Yellow
$binaryPath = Join-Path $InstallPath "SentinelKiosk.Agent.exe"

# Check if service exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "  Service already exists, stopping..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

# Create service
New-Service -Name $ServiceName `
    -BinaryPathName "`"$binaryPath`"" `
    -DisplayName $ServiceDisplayName `
    -Description $ServiceDescription `
    -StartupType Automatic | Out-Null

Write-Host "  Service installed" -ForegroundColor Green

# Set service recovery options (restart on failure)
Write-Host "Configuring service recovery..." -ForegroundColor Yellow
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null
Write-Host "  Recovery options set" -ForegroundColor Green

# Set NTFS permissions on ProgramData (restrict to Administrators and SYSTEM)
Write-Host "Setting directory permissions..." -ForegroundColor Yellow
$acl = Get-Acl $programDataPath
$acl.SetAccessRuleProtection($true, $false)  # Disable inheritance, remove inherited rules

# Administrators: Full control
$adminRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    "BUILTIN\Administrators", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
$acl.AddAccessRule($adminRule)

# SYSTEM: Full control
$systemRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    "NT AUTHORITY\SYSTEM", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
$acl.AddAccessRule($systemRule)

# Service SID: Read/Write (if using virtual account)
# $serviceRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
#     "NT SERVICE\$ServiceName", "Modify", "ContainerInherit,ObjectInherit", "None", "Allow")
# $acl.AddAccessRule($serviceRule)

Set-Acl -Path $programDataPath -AclObject $acl
Write-Host "  Permissions set" -ForegroundColor Green

# Start service
Write-Host "Starting service..." -ForegroundColor Yellow
Start-Service -Name $ServiceName
Start-Sleep -Seconds 3

$service = Get-Service -Name $ServiceName
if ($service.Status -eq "Running") {
    Write-Host "  Service is running" -ForegroundColor Green
} else {
    Write-Host "  WARNING: Service failed to start. Status: $($service.Status)" -ForegroundColor Red
    Write-Host "  Check logs at: $programDataPath\Logs\" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Installation Complete ===" -ForegroundColor Cyan
Write-Host "Server URL: $ServerUrl" -ForegroundColor White
Write-Host "Install Path: $InstallPath" -ForegroundColor White
Write-Host "Data Path: $programDataPath" -ForegroundColor White
Write-Host "Service Name: $ServiceName" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Verify enrollment in the admin console" -ForegroundColor White
Write-Host "2. Assign a policy to this device" -ForegroundColor White
Write-Host "3. Monitor status in Dashboard" -ForegroundColor White
Write-Host ""
Write-Host "To uninstall: .\uninstall.ps1" -ForegroundColor Yellow
