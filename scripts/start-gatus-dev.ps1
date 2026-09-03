# Starts Gatus API (5163) + Admin UI (5173) as persistent background processes
$root = "C:\Users\001adm_am\OneDrive - Living Spaces\Documents\GitHub\GatUs"

# API
$apiRunning = Get-NetTCPConnection -LocalPort 5163 -State Listen -ErrorAction SilentlyContinue
if (-not $apiRunning) {
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:ASPNETCORE_URLS = "http://0.0.0.0:5163"
    Start-Process -FilePath "C:\Users\001adm_am\dotnet\dotnet.exe" `
        -ArgumentList "run --no-launch-profile" `
        -WorkingDirectory "$root\apps\api-server" `
        -WindowStyle Hidden
    Write-Host "API starting..."
} else { Write-Host "API already running" }

# Admin UI
$uiRunning = Get-NetTCPConnection -LocalPort 5173 -State Listen -ErrorAction SilentlyContinue
if (-not $uiRunning) {
    Start-Process -FilePath "cmd.exe" `
        -ArgumentList "/c npm run dev" `
        -WorkingDirectory "$root\apps\admin-web" `
        -WindowStyle Hidden
    Write-Host "UI starting..."
} else { Write-Host "UI already running" }

Start-Sleep 12
$api = Get-NetTCPConnection -LocalPort 5163 -State Listen -ErrorAction SilentlyContinue
$ui  = Get-NetTCPConnection -LocalPort 5173 -State Listen -ErrorAction SilentlyContinue
Write-Host "API 5163: $(if ($api) {'UP'} else {'DOWN'})"
Write-Host "UI  5173: $(if ($ui) {'UP'} else {'DOWN'})"
