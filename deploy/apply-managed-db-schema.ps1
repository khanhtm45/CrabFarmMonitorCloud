# Áp schema lên DigitalOcean Managed PostgreSQL (chạy một lần hoặc sau khi đổi SQL)
# Usage:
#   .\deploy\apply-managed-db-schema.ps1 -Password "your-doadmin-password"
#   .\deploy\apply-managed-db-schema.ps1 -EnvFile "d:\path\.env"   # đọc DATABASE_URL

param(
    [string]$DbHost = "ras-cloud-do-user-37760190-0.g.db.ondigitalocean.com",
    [int]$Port = 25060,
    [string]$Database = "defaultdb",
    [string]$Username = "doadmin",
    [string]$Password = "",
    [string]$EnvFile = "",
    [switch]$SkipDomain,
    [switch]$Fresh
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path $PSScriptRoot -Parent
$Docs = Join-Path $RepoRoot "database"
$Init03 = Join-Path $RepoRoot "database\03-crab-boxes.sql"

function Get-DatabaseUrlFromEnvFile([string]$path) {
    if (-not (Test-Path $path)) { return $null }
    foreach ($line in Get-Content $path) {
        if ($line -match '^\s*DATABASE_URL\s*=\s*(.+)\s*$') {
            return $Matches[1].Trim()
        }
    }
    return $null
}

function Parse-NpgsqlUrl([string]$url) {
    $p = @{ DbHost = ""; Port = 5432; Database = ""; Username = ""; Password = "" }
    foreach ($part in $url.Split(';')) {
        $kv = $part.Split('=', 2)
        if ($kv.Length -lt 2) { continue }
        $k = $kv[0].Trim()
        $v = $kv[1].Trim()
        switch -Regex ($k) {
            '^Host$' { $p.DbHost = $v }
            '^Port$' { $p.Port = [int]$v }
            '^Database$' { $p.Database = $v }
            '^Username$' { $p.Username = $v }
            '^Password$' { $p.Password = $v }
        }
    }
    return $p
}

if ($EnvFile) {
    $dbUrl = Get-DatabaseUrlFromEnvFile $EnvFile
    if (-not $dbUrl) { throw "DATABASE_URL not found in $EnvFile" }
    $parsed = Parse-NpgsqlUrl $dbUrl
    $DbHost = $parsed.DbHost
    $Port = $parsed.Port
    $Database = $parsed.Database
    $Username = $parsed.Username
    $Password = $parsed.Password
}

if ([string]::IsNullOrWhiteSpace($Password)) {
    $sec = Read-Host "PostgreSQL password ($Username)" -AsSecureString
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
    try { $Password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}

$encodedPass = [uri]::EscapeDataString($Password)
$pgUri = "postgresql://${Username}:${encodedPass}@${DbHost}:${Port}/${Database}?sslmode=require"

$files = @(
    (Join-Path $Docs "cloud_postgresql_schema.sql"),
    (Join-Path $Docs "cloud_domain_schema.sql"),
    $Init03
)

Write-Host "Applying schema to ${DbHost}:${Port}/${Database} ..." -ForegroundColor Cyan

if ($Fresh) {
    $reset = Join-Path $PSScriptRoot "reset-managed-db-schema.sql"
    if (-not (Test-Path $reset)) { throw "Missing $reset" }
    Write-Host "  -> reset-managed-db-schema.sql (drop old tables)" -ForegroundColor Yellow
    docker run --rm -v "${reset}:/schema.sql:ro" postgres:16-alpine psql "$pgUri" -v ON_ERROR_STOP=1 -f /schema.sql
    if ($LASTEXITCODE -ne 0) { throw "reset failed (exit $LASTEXITCODE)" }
}

foreach ($f in $files) {
    if (-not (Test-Path $f)) { throw "Missing SQL file: $f" }
    $name = Split-Path $f -Leaf
    if ($SkipDomain -and $name -eq "cloud_domain_schema.sql") { continue }
    Write-Host "  -> $name" -ForegroundColor Gray
    $mount = "${f}:/schema.sql:ro"
    docker run --rm -v $mount postgres:16-alpine psql "$pgUri" -v ON_ERROR_STOP=1 -f /schema.sql
    if ($LASTEXITCODE -ne 0) { throw "psql failed on $name (exit $LASTEXITCODE)" }
}

Write-Host "Done. Verify: SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY 1;" -ForegroundColor Green
