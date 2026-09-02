# =============================================================================
#  Chess Realtime Assistant — Windows Setup Script
#  Chạy bằng PowerShell với quyền Administrator
# =============================================================================
#  Cách chạy:
#    1. Nhấn chuột phải vào file → "Run with PowerShell"
#    2. Hoặc mở PowerShell (Admin) và gõ:
#         Set-ExecutionPolicy Bypass -Scope Process -Force
#         .\setup_windows.ps1
# =============================================================================
#Requires -Version 5.1

$ErrorActionPreference = "Stop"

# ─── Colors & helpers ────────────────────────────────────────────────────────
function Write-Step($msg) {
    Write-Host ""
    Write-Host "══════════════════════════════════════════" -ForegroundColor Magenta
    Write-Host "  $msg" -ForegroundColor Cyan
    Write-Host "══════════════════════════════════════════" -ForegroundColor Magenta
}
function Write-OK($msg)   { Write-Host "[OK]   $msg" -ForegroundColor Green }
function Write-INFO($msg) { Write-Host "[INFO] $msg" -ForegroundColor Cyan }
function Write-WARN($msg) { Write-Host "[WARN] $msg" -ForegroundColor Yellow }
function Write-ERR($msg)  { Write-Host "[ERR]  $msg" -ForegroundColor Red }

Clear-Host
Write-Host @"
  ██████╗██╗  ██╗███████╗███████╗███████╗
  ██╔════╝██║  ██║██╔════╝██╔════╝██╔════╝
  ██║     ███████║█████╗  ███████╗███████╗
  ██║     ██╔══██║██╔══╝  ╚════██║╚════██║
  ╚██████╗██║  ██║███████╗███████║███████║
"@ -ForegroundColor Magenta
Write-Host "  Chess Realtime Assistant — Windows Setup" -ForegroundColor Cyan
Write-Host "  Hỗ trợ: Windows 10 / 11 (x64)" -ForegroundColor Green
Write-Host ""

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

# ─── Step 1: Check PowerShell & OS ───────────────────────────────────────────
Write-Step "1/5 — Kiểm tra hệ thống"

$OSVersion = [System.Environment]::OSVersion.Version
Write-INFO "Windows version: $($OSVersion.Major).$($OSVersion.Minor) (Build $($OSVersion.Build))"

if ($OSVersion.Major -lt 10) {
    Write-ERR "Cần Windows 10 trở lên. Phiên bản hiện tại không được hỗ trợ."
    pause; exit 1
}

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-WARN "Script không chạy với quyền Administrator."
    Write-WARN "Một số bước có thể yêu cầu quyền Admin. Tiếp tục với quyền hiện tại..."
    Write-Host ""
}

# Check architecture
$arch = (Get-CimInstance Win32_OperatingSystem).OSArchitecture
Write-INFO "Architecture: $arch"
if ($arch -notlike "*64*") {
    Write-WARN "Hệ thống 32-bit có thể không hỗ trợ Stockfish binary mới nhất."
}

# ─── Step 2: Install .NET 9 SDK ──────────────────────────────────────────────
Write-Step "2/5 — Cài đặt .NET 9 SDK"

$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
$needDotnet = $true

if ($dotnetCmd) {
    $dotnetSdks = & dotnet --list-sdks 2>$null
    if ($dotnetSdks -match "(?m)^9\.") {
        Write-OK ".NET 9 đã cài đặt."
        $needDotnet = $false
    } else {
        $dotnetVer = & dotnet --version 2>$null
        Write-WARN ".NET version hiện tại: $dotnetVer (cần 9.x)"
    }
}

if ($needDotnet) {
    Write-INFO "Đang tải .NET 9 SDK..."

    # Try winget first (available on Win 10 1709+ / Win 11)
    if (Get-Command winget -ErrorAction SilentlyContinue) {
        Write-INFO "Cài qua winget..."
        winget install Microsoft.DotNet.SDK.9 --silent --accept-package-agreements --accept-source-agreements
    } else {
        # Fallback: download installer directly
        Write-INFO "winget không có sẵn. Tải installer thủ công..."
        $dotnetUrl = "https://download.visualstudio.microsoft.com/download/pr/dotnet-sdk-9.0-win-x64.exe"
        $dotnetInstaller = "$env:TEMP\dotnet-sdk-9-installer.exe"
        Write-INFO "Đang tải từ: $dotnetUrl"
        Invoke-WebRequest -Uri $dotnetUrl -OutFile $dotnetInstaller -UseBasicParsing
        Write-INFO "Đang cài đặt (silent)..."
        Start-Process -FilePath $dotnetInstaller -ArgumentList "/install", "/quiet", "/norestart" -Wait
        Remove-Item $dotnetInstaller -ErrorAction SilentlyContinue

        # Refresh PATH
        $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" +
                    [System.Environment]::GetEnvironmentVariable("PATH", "User")
    }

    if (Get-Command dotnet -ErrorAction SilentlyContinue) {
        Write-OK ".NET cài thành công: $(dotnet --version)"
    } else {
        Write-ERR ".NET cài thất bại. Tải thủ công tại: https://dotnet.microsoft.com/download/dotnet/9.0"
        Write-ERR "Sau đó chạy lại script này."
        pause; exit 1
    }
}

# ─── Step 3: Install Stockfish ───────────────────────────────────────────────
Write-Step "3/5 — Tải Stockfish Engine"

$engineDir  = Join-Path $ScriptDir "brain-backend\Engine"
$stockfishExe = Join-Path $engineDir "stockfish.exe"

if (Test-Path $stockfishExe) {
    Write-OK "stockfish.exe đã có tại: $stockfishExe"
} else {
    Write-INFO "Tải Stockfish 17 cho Windows x64..."
    New-Item -ItemType Directory -Force -Path $engineDir | Out-Null

    $zipUrl  = "https://github.com/official-stockfish/Stockfish/releases/download/sf_17/stockfish-windows-x86-64-avx2.zip"
    $zipPath = "$env:TEMP\stockfish.zip"
    $tmpDir  = "$env:TEMP\stockfish_extract"

    try {
        Write-INFO "Đang tải: $zipUrl"
        Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath -UseBasicParsing -TimeoutSec 120
        Write-INFO "Giải nén..."
        Expand-Archive -Path $zipPath -DestinationPath $tmpDir -Force
        $exeFound = Get-ChildItem -Path $tmpDir -Recurse -Filter "stockfish*.exe" | Select-Object -First 1
        if ($null -eq $exeFound) {
            throw "Không tìm thấy stockfish.exe trong archive."
        }
        Copy-Item -Path $exeFound.FullName -Destination $stockfishExe -Force
        Write-OK "Stockfish đã cài tại: $stockfishExe"
    } catch {
        Write-ERR "Tải Stockfish thất bại: $_"
        Write-WARN "Tải thủ công tại: https://github.com/official-stockfish/Stockfish/releases"
        Write-WARN "Đặt stockfish.exe vào: brain-backend\Engine\"
    } finally {
        Remove-Item $zipPath -ErrorAction SilentlyContinue
        Remove-Item $tmpDir -Recurse -ErrorAction SilentlyContinue
    }
}

# ─── Step 4: Restore .NET packages ───────────────────────────────────────────
Write-Step "4/5 — Restore .NET packages"

$backendDir = Join-Path $ScriptDir "brain-backend"
Set-Location $backendDir
Write-INFO "Chạy dotnet restore..."
& dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-ERR "dotnet restore thất bại. Kiểm tra kết nối internet."
    pause; exit 1
}
Write-OK "dotnet restore hoàn tất."
Set-Location $ScriptDir

# ─── Step 5: Verify & Summary ────────────────────────────────────────────────
Write-Step "5/5 — Hoàn tất"

$checks = @(
    @{ Name = ".NET 9";     OK = (Get-Command dotnet -EA SilentlyContinue) -and ((& dotnet --list-sdks) -match "(?m)^9\.") },
    @{ Name = "Stockfish";  OK = Test-Path $stockfishExe }
)

Write-Host ""
foreach ($c in $checks) {
    if ($c.OK) { Write-OK  "$($c.Name) ✓" }
    else        { Write-ERR "$($c.Name) ✗ — cần xem lại" }
}

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║           ✅  Setup hoàn tất thành công!             ║" -ForegroundColor Green
Write-Host "╚══════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "  Để khởi chạy assistant:" -ForegroundColor White
Write-Host "    Double-click: START_HERE.bat" -ForegroundColor Cyan
Write-Host "    Hoặc: start_assistant.bat" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Sau đó vào Chrome → chrome://extensions/ → Load unpacked" -ForegroundColor White
Write-Host "  → chọn thư mục: chrome-extension\" -ForegroundColor Cyan
Write-Host ""

pause
