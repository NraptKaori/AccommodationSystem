param(
    [string]$Version      = "1.0.0",
    [string]$IsccPath     = "",   # 例: -IsccPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    [string]$CertThumb    = "",   # 証明書サムプリント (certmgr.msc で確認)
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$BuildDir    = Join-Path $ProjectRoot "bin\x64\Release\net48"
$IssFile     = Join-Path $PSScriptRoot "setup.iss"

function Find-Iscc {
    # Refresh PATH from registry
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" +
                [System.Environment]::GetEnvironmentVariable("Path","User")

    $candidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 5\ISCC.exe",
        "C:\Program Files\Inno Setup 5\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 5\ISCC.exe",
        "$env:APPDATA\Inno Setup 6\ISCC.exe"
    )
    foreach ($p in $candidates) {
        if ($p -and (Test-Path $p)) { return $p }
    }

    # Search recursively (slow but thorough)
    foreach ($root in @("C:\Program Files (x86)", "C:\Program Files", $env:LOCALAPPDATA)) {
        if (Test-Path $root) {
            $hit = Get-ChildItem -Path $root -Filter "ISCC.exe" -Recurse -ErrorAction SilentlyContinue |
                   Select-Object -First 1
            if ($hit) { return $hit.FullName }
        }
    }

    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    return $null
}

Write-Host "=== Build: AccommodationSystem v$Version ===" -ForegroundColor Cyan

# --- Step 1: dotnet build ---
Write-Host "[1/3] dotnet build ..." -ForegroundColor Yellow
Push-Location $ProjectRoot
try {
    dotnet build AccommodationSystem.csproj -c Release -p:Platform=x64 --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)" }
} finally {
    Pop-Location
}
$exePath = Join-Path $BuildDir "AccommodationSystem.exe"
if (-not (Test-Path $exePath)) { throw "Build output not found: $exePath" }
Write-Host "Build OK: $BuildDir" -ForegroundColor Green

# --- Step 2: Inno Setup ---
Write-Host "[2/3] Checking Inno Setup ..." -ForegroundColor Yellow

# Use override path if specified
if ($IsccPath -and (Test-Path $IsccPath)) {
    Write-Host "Using specified path: $IsccPath" -ForegroundColor Green
} else {
    $IsccPath = Find-Iscc
    if (-not $IsccPath) {
        Write-Host "Not found in standard locations. Trying winget ..."
        winget install JRSoftware.InnoSetup --silent --accept-package-agreements --accept-source-agreements
        $IsccPath = Find-Iscc
    }
}

if (-not $IsccPath) {
    Write-Host ""
    Write-Host "[ERROR] ISCC.exe not found." -ForegroundColor Red
    Write-Host ""
    Write-Host "Run this to search manually:" -ForegroundColor Yellow
    Write-Host "  Get-ChildItem C:\ -Filter ISCC.exe -Recurse -ErrorAction SilentlyContinue | Select FullName" -ForegroundColor White
    Write-Host ""
    Write-Host "Then re-run with the path:" -ForegroundColor Yellow
    Write-Host "  .\installer\build.ps1 -IsccPath `"C:\path\to\ISCC.exe`"" -ForegroundColor White
    exit 1
}
Write-Host "Inno Setup: $IsccPath" -ForegroundColor Green

# --- Step 3: Update version & compile ---
Write-Host "[3/3] Compiling installer (v$Version) ..." -ForegroundColor Yellow
$iss = [System.IO.File]::ReadAllText($IssFile, [System.Text.Encoding]::UTF8)
$iss = $iss -replace '#define MyAppVersion\s+"[^"]+"', "#define MyAppVersion `"$Version`""
[System.IO.File]::WriteAllText($IssFile, $iss, [System.Text.Encoding]::UTF8)

& $IsccPath $IssFile
if ($LASTEXITCODE -ne 0) { throw "ISCC.exe failed (exit $LASTEXITCODE)" }

$OutputExe = Join-Path $PSScriptRoot "Output\AccommodationSystem-Setup-$Version.exe"

# --- Step 4: Code signing (optional) ---
if ($CertThumb) {
    Write-Host "[4/4] Signing installer ..." -ForegroundColor Yellow
    $signtool = Get-ChildItem "C:\Program Files*\Windows Kits\*\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $signtool) { throw "signtool.exe not found. Install Windows SDK." }

    & $signtool.FullName sign /sha1 $CertThumb /tr $TimestampUrl /td sha256 /fd sha256 $OutputExe
    if ($LASTEXITCODE -ne 0) { throw "Signing failed." }
    Write-Host "Signed OK." -ForegroundColor Green
}

Write-Host ""
Write-Host "=== Done ===" -ForegroundColor Green
Write-Host "Installer: $OutputExe" -ForegroundColor Cyan
