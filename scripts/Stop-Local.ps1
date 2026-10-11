<#
.SYNOPSIS
    Stops all local Golf League processes (Functions host, Vite web).
#>

[CmdletBinding()]
param()

Write-Host "Stopping Golf League local services..." -ForegroundColor Yellow

# Kill process on port 7071 (Functions host)
$port7071 = Get-NetTCPConnection -LocalPort 7071 -ErrorAction SilentlyContinue
if ($port7071) {
    Stop-Process -Id $port7071.OwningProcess -Force -ErrorAction SilentlyContinue
    Write-Host "Stopped Azure Functions API (port 7071)." -ForegroundColor Green
} else {
    Write-Host "Port 7071 is not in use." -ForegroundColor Gray
}

# Kill node process on port 5173 (Vite dev server)
$port5173 = Get-NetTCPConnection -LocalPort 5173 -ErrorAction SilentlyContinue
if ($port5173) {
    Stop-Process -Id $port5173.OwningProcess -Force -ErrorAction SilentlyContinue
    Write-Host "Stopped Vite frontend (port 5173)." -ForegroundColor Green
} else {
    Write-Host "Port 5173 is not in use." -ForegroundColor Gray
}

Write-Host "All local services stopped." -ForegroundColor Green
