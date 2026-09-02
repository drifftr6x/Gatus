# Generates a self-signed TLS certificate for local/lab use of the production stack.
# Output: infrastructure/certs/server.crt + server.key (mounted by nginx).
# For real deployments, replace these with certificates from your CA.
#
# Prefers openssl (present in Git Bash / most dev machines); falls back to
# Windows cert store + .NET export on older PowerShell.

$ErrorActionPreference = 'Stop'

$certsDir = Join-Path (Join-Path $PSScriptRoot '..') 'certs'
New-Item -ItemType Directory -Force -Path $certsDir | Out-Null

$hostname = if ($args[0]) { $args[0] } else { 'localhost' }
$crtPath = Join-Path $certsDir 'server.crt'
$keyPath = Join-Path $certsDir 'server.key'

$openssl = Get-Command openssl -ErrorAction SilentlyContinue
if ($openssl) {
    & openssl req -x509 -newkey rsa:2048 -nodes `
        -keyout $keyPath -out $crtPath `
        -days 730 -subj "/CN=$hostname" `
        -addext "subjectAltName=DNS:$hostname" 2>$null
    if ($LASTEXITCODE -ne 0) { throw "openssl failed with exit code $LASTEXITCODE" }
}
else {
    # Fallback: Windows cert store + PFX export (requires openssl for PEM split,
    # so fail with a clear message instead)
    throw "openssl not found. Install Git for Windows (includes openssl) or provide certs manually in $certsDir"
}

Write-Host "Created self-signed cert for '$hostname':"
Write-Host "  $crtPath"
Write-Host "  $keyPath"
Write-Host ""
Write-Host "Browsers will warn about the untrusted cert - that is expected for lab use."
