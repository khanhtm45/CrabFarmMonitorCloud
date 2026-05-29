# Chạy Cloud API trên localhost:8080 (đọc .env ở thư mục repo)
$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
Set-Location $Root

if (-not (Test-Path (Join-Path $Root ".env"))) {
    Write-Host "Chưa có .env — copy từ .env.example rồi điền DB_PASSWORD, JWT_SECRET" -ForegroundColor Yellow
    Copy-Item (Join-Path $Root ".env.example") (Join-Path $Root ".env")
}

$env:HTTP_PORT = "8080"
$env:ASPNETCORE_ENVIRONMENT = "Development"

Write-Host "Starting CrabFarm Cloud at http://localhost:8080" -ForegroundColor Cyan
Write-Host "Health: http://localhost:8080/health" -ForegroundColor Gray
Write-Host "Desktop .env: CLOUD_API_URL=http://localhost:8080" -ForegroundColor Gray

dotnet run --project (Join-Path $Root "src\CrabFarmMonitor.Cloud\CrabFarmMonitor.Cloud.csproj")
