# Download Stockfish Engine
# Chạy script này một lần để tải Stockfish 16 cho Windows.
# Script tự động đặt stockfish.exe vào đúng thư mục Engine/

$ErrorActionPreference = "Stop"

$engineDir = "$PSScriptRoot\Engine"
$exePath   = "$engineDir\stockfish.exe"

if (Test-Path $exePath) {
    Write-Host "[OK] stockfish.exe already exists at: $exePath" -ForegroundColor Green
    exit 0
}

Write-Host "[INFO] Downloading Stockfish 16 for Windows x64..." -ForegroundColor Cyan

$zipUrl  = "https://github.com/official-stockfish/Stockfish/releases/download/sf_16/stockfish-windows-x86-64-avx2.zip"
$zipPath = "$env:TEMP\stockfish.zip"
$tmpDir  = "$env:TEMP\stockfish_extract"

try {
    Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath -UseBasicParsing
    Write-Host "[INFO] Download complete. Extracting..."

    Expand-Archive -Path $zipPath -DestinationPath $tmpDir -Force

    $exeFound = Get-ChildItem -Path $tmpDir -Recurse -Filter "stockfish*.exe" | Select-Object -First 1
    if (-not $exeFound) {
        Write-Error "stockfish.exe not found inside zip. Check the download URL."
    }

    New-Item -ItemType Directory -Force -Path $engineDir | Out-Null
    Copy-Item -Path $exeFound.FullName -Destination $exePath -Force

    Write-Host "[OK] stockfish.exe installed at: $exePath" -ForegroundColor Green
}
finally {
    Remove-Item -Path $zipPath -ErrorAction SilentlyContinue
    Remove-Item -Path $tmpDir -Recurse -ErrorAction SilentlyContinue
}
