<#
.SYNOPSIS
    Starts the Golf League application locally (LocalDB + Azure Functions API + Vite Web).
.DESCRIPTION
    1. Ensures SQL Server LocalDB (MSSQLLocalDB) is running.
    2. Builds the backend if needed and ensures local.settings.json is synced.
    3. Launches the Functions host on port 7071 in a background job or terminal.
    4. Waits for the API health check (http://localhost:7071/api/health).
    5. Displays test credentials and opens the browser.
    6. Launches the Vite development server on port 5173.
#>

[CmdletBinding()]
param(
    [switch]$NoBrowser,
    [switch]$Rebuild
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "=== Starting Golf League Local Environment ===" -ForegroundColor Cyan

# 1. Check & start LocalDB
Write-Host "Checking SQL Server LocalDB..." -ForegroundColor Yellow
$localDbStatus = sqllocaldb info MSSQLLocalDB 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "MSSQLLocalDB not found. Creating instance..." -ForegroundColor Yellow
    sqllocaldb create MSSQLLocalDB
}
sqllocaldb start MSSQLLocalDB
Write-Host "SQL Server LocalDB is running." -ForegroundColor Green

# 2. Build backend if requested or missing
$FunctionsDir = Join-Path $RepoRoot "src\GolfLeague.Functions"
$BinDir = Join-Path $FunctionsDir "bin\Debug\net10.0"
$FunctionsDll = Join-Path $BinDir "GolfLeague.Functions.dll"

if ($Rebuild -or (-not (Test-Path $FunctionsDll))) {
    Write-Host "Building GolfLeague.Functions (.NET 10)..." -ForegroundColor Yellow
    dotnet build $FunctionsDir
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Backend build failed."
        exit 1
    }
}

# Ensure local.settings.json is copied to bin
Copy-Item (Join-Path $FunctionsDir "local.settings.json") (Join-Path $BinDir "local.settings.json") -Force

# 3. Stop any existing process on port 7071
$port7071 = Get-NetTCPConnection -LocalPort 7071 -ErrorAction SilentlyContinue
if ($port7071) {
    Write-Host "Stopping existing process on port 7071..." -ForegroundColor Yellow
    Stop-Process -Id $port7071.OwningProcess -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
}

# 4. Launch Functions Host
Write-Host "Starting Azure Functions API on http://localhost:7071..." -ForegroundColor Yellow
$funcProcess = Start-Process -FilePath "cmd.exe" -ArgumentList "/c", "func host start --no-build --port 7071" -WorkingDirectory $BinDir -PassThru -WindowStyle Minimized

# 5. Wait for API Health
Write-Host "Waiting for API to become healthy..." -ForegroundColor Yellow
$healthy = $false
for ($i = 0; $i -lt 30; $i++) {
    try {
        $res = Invoke-RestMethod -Uri "http://localhost:7071/api/health" -TimeoutSec 2 -ErrorAction Stop
        if ($res.status -eq "healthy") {
            $healthy = $true
            break
        }
    }
    catch {
        Start-Sleep -Seconds 1
    }
}

if (-not $healthy) {
    Write-Error "API failed to report healthy within 30 seconds."
    exit 1
}
Write-Host "API is healthy and ready on http://localhost:7071!" -ForegroundColor Green

# 6. Display Test Credentials
Write-Host ""
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "               LOCAL TEST CREDENTIALS                     " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Admin:  admin@golfleague.local  / Password123!" -ForegroundColor White
Write-Host " Scorer: scorer@golfleague.local / Password123!" -ForegroundColor White
Write-Host " Player: player@golfleague.local / Password123!" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

# 7. Start Web App
$WebDir = Join-Path $RepoRoot "web"
if (-not $NoBrowser) {
    Start-Process "http://localhost:5173"
}

Write-Host "Starting Vite frontend on http://localhost:5173..." -ForegroundColor Cyan
Write-Host "Press Ctrl+C in this terminal to stop Vite. (To stop the API, run scripts\Stop-Local.ps1)" -ForegroundColor Gray
Set-Location $WebDir
npm run dev
