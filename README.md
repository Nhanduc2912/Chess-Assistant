# ♟️ Chess Realtime Assistant

> **Một extension chạy hoàn toàn local trên máy bạn** — không có server, không gửi dữ liệu ra ngoài, phân tích bàn cờ thời gian thực từ Chess.com & Lichess bằng Stockfish engine.

![Version](https://img.shields.io/badge/version-2.0-blue)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux-lightgrey)
![License](https://img.shields.io/badge/license-MIT-green)
![.NET](https://img.shields.io/badge/.NET-9.0-purple)
![Stockfish](https://img.shields.io/badge/Stockfish-17-orange)

---

## 🎯 Giới thiệu

Chess Realtime Assistant là **Chrome Extension** kết hợp với **backend cục bộ** chạy Stockfish — engine cờ vua mạnh nhất thế giới. Toàn bộ tính toán diễn ra **trên máy bạn**, không phụ thuộc vào bất kỳ server hay API bên ngoài nào.

**Cách hoạt động đơn giản:**

```
Chess.com / Lichess  →  Chrome Extension  →  Backend Local (Stockfish)  →  Gợi ý nước đi
       (DOM)              (content.js)           (localhost:5000)              (Overlay UI)
```

**Tính năng chính:**

| | Tính năng |
|---|---|
| ⚡ | **Phân tích thời gian thực** — tự động gửi FEN sau mỗi nước đi |
| 🧠 | **Stockfish 17** — MultiPV 3 đường, cache thông minh |
| 🔌 | **Chrome Extension** — tích hợp trực tiếp vào Chess.com & Lichess |
| 📊 | **Phân loại nước đi** — Brilliant / Best / Good / Inaccuracy / Mistake / Blunder |
| 🔒 | **100% local** — không gửi data ra ngoài, không cần tài khoản |

---

## 🚀 Cài đặt nhanh

> Chọn hệ điều hành của bạn:

### 🐧 Linux (Ubuntu · Debian · Mint · Arch · Fedora · openSUSE)

```bash
# 1. Clone repo
git clone https://github.com/Nhanduc2912/Chess-Assistant.git
cd Chess-Assistant/ChessAssistantRoot

# 2. Chạy script setup (tự động cài .NET 9, Stockfish, restore packages)
chmod +x setup_linux.sh
bash setup_linux.sh

# 3. Khởi chạy
bash START_HERE.sh
```

### 🪟 Windows 10 / 11

```powershell
# 1. Clone repo
git clone https://github.com/Nhanduc2912/Chess-Assistant.git
cd Chess-Assistant\ChessAssistantRoot

# 2. Chạy script setup (PowerShell — có thể cần chuột phải → "Run as Administrator")
.\setup_windows.ps1

# 3. Khởi chạy
.\START_HERE.bat
```

> 💡 **Sau khi backend đang chạy** (hiện `http://localhost:5000` trên terminal), tiếp tục bước cài Chrome Extension bên dưới.

### 🔌 Cài Chrome Extension (cả 2 OS)

1. Mở Chrome → vào `chrome://extensions/`
2. Bật **Developer mode** (góc trên phải)
3. Click **Load unpacked** → chọn thư mục `chrome-extension/`
4. Icon ♟️ xuất hiện trên toolbar → Click → thấy **"Stockfish Online"** ✅
5. Vào Chess.com / Lichess → bắt đầu ván → mỗi nước đi tự động được phân tích!

> ⚠️ **Nếu thấy "Content script not loaded"** → nhấn **F5** để reload trang chess.

---

## ⚙️ Setup chi tiết

### 🐧 Setup cho Linux

Script `setup_linux.sh` tự động nhận diện distro và thực hiện toàn bộ:

| Bước | Nội dung |
|------|----------|
| **0** | Nhận diện distro: Ubuntu, Debian, Mint, Arch, Fedora, openSUSE... |
| **1** | Cài curl, wget, git, unzip nếu thiếu |
| **2** | Cài **.NET 9 SDK** qua package manager phù hợp |
| **3** | Cài **Stockfish** (package manager hoặc tải binary từ GitHub) |
| **4** | `dotnet restore` packages cho brain-backend |
| **5** | Lưu cấu hình vào `.env`, in hướng dẫn khởi chạy |

**Distro cụ thể:**

<details>
<summary>Ubuntu / Linux Mint / Debian / Pop!_OS</summary>

Script dùng Microsoft APT feed để cài .NET 9:
```bash
# Hoặc cài thủ công .NET 9:
sudo apt-get update
sudo apt-get install -y dotnet-sdk-9.0
sudo apt-get install -y stockfish
```
</details>

<details>
<summary>Arch Linux / Manjaro / EndeavourOS</summary>

Script dùng `pacman` / `yay` / `paru` tùy AUR helper có sẵn:
```bash
# Thủ công:
sudo pacman -S dotnet-sdk stockfish
```
</details>

<details>
<summary>Fedora / RHEL / AlmaLinux</summary>

```bash
# Thủ công:
sudo dnf install -y dotnet-sdk-9.0 stockfish
```
</details>

<details>
<summary>openSUSE</summary>

```bash
# Thủ công:
sudo zypper install -y dotnet-sdk-9.0 stockfish
```
</details>

---

### 🪟 Setup cho Windows

Script `setup_windows.ps1` tự động:

| Bước | Nội dung |
|------|----------|
| **1** | Kiểm tra Windows version và architecture |
| **2** | Cài **.NET 9 SDK** qua `winget` (hoặc installer nếu winget chưa có) |
| **3** | Tải **Stockfish 17** `.zip` từ GitHub và đặt vào `brain-backend\Engine\` |
| **4** | `dotnet restore` packages cho brain-backend |
| **5** | In checklist kết quả và hướng dẫn tiếp theo |

**Chạy script:**
```powershell
# Mở PowerShell với quyền Administrator, rồi:
Set-ExecutionPolicy Bypass -Scope Process -Force
.\setup_windows.ps1
```

> Nếu không muốn dùng script, xem hướng dẫn cài thủ công ở phần [Manual Setup](#manual-setup).

---

## ▶️ Khởi chạy

### 🐧 Linux — RunQuick

```bash
cd Chess-Assistant/ChessAssistantRoot

# Chạy Backend + auto-detect Stockfish path:
bash START_HERE.sh
```

Hoặc khởi chạy thủ công:
```bash
cd brain-backend
dotnet run
```

### 🪟 Windows — RunQuick

```bat
REM Double-click hoặc chạy trong CMD:
START_HERE.bat
```

Hoặc khởi chạy thủ công:
```powershell
cd brain-backend
dotnet run
```

**Backend URL:** `http://localhost:5000`  
**Health check:** `http://localhost:5000/api/analyze/status`  
**Diagnostics:** `http://localhost:5000/api/analyze/diagnostics`

---

## 🔧 Manual Setup

<details>
<summary>Cài thủ công (không dùng script)</summary>

### 1. Yêu cầu

| Thành phần | Yêu cầu |
|---|---|
| .NET SDK | 9.0+ |
| Chrome | 110+ |
| Stockfish | 16 / 17 |
| Python | 3.11+ *(chỉ nếu dùng Vision Module)* |

### 2. Cài .NET 9

- **Windows**: [dotnet.microsoft.com/download/dotnet/9.0](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Linux**: Theo distro — xem [learn.microsoft.com/dotnet/core/install/linux](https://learn.microsoft.com/dotnet/core/install/linux)

### 3. Tải Stockfish

- **Windows**: Tải `.zip` từ [Stockfish Releases](https://github.com/official-stockfish/Stockfish/releases) → giải nén → đặt `stockfish.exe` vào `brain-backend/Engine/`
- **Linux**: `sudo apt install stockfish` hoặc `sudo pacman -S stockfish`

### 4. Restore & Run

```bash
cd brain-backend
dotnet restore
dotnet run
```

</details>

---

## ⚙️ Cấu hình Engine

Chỉnh trong `brain-backend/appsettings.json`:

```json
{
  "Stockfish": {
    "EnginePath": "Engine/stockfish.exe",
    "MultiPV": 3,
    "Depth": 12,
    "Threads": 4,
    "HashMB": 256,
    "TimeoutMs": 5000
  }
}
```

| Tham số | Mô tả | Gợi ý |
|---|---|---|
| `Threads` | Số nhân CPU dùng cho Stockfish | Số nhân / 2 |
| `HashMB` | RAM cache cho engine | 256–512 MB |
| `TimeoutMs` | Timeout mỗi nước phân tích | 5000ms |
| `MultiPV` | Số nước đi gợi ý song song | 3 |

---

## 📊 Phân loại nước đi

| Phân loại | Delta (Centipawns) | Màu |
|---|---|---|
| 💡 **Brilliant** | `Δ > 0` | 🟣 Tím |
| ⭐ **Best** | `Δ = 0` | 🟢 Xanh đậm |
| ✅ **Good** | `-50 ≤ Δ < 0` | 🟢 Xanh nhạt |
| ⚠️ **Inaccuracy** | `-150 ≤ Δ < -50` | 🟡 Vàng |
| ❌ **Mistake** | `-300 ≤ Δ < -150` | 🟠 Cam |
| 💀 **Blunder** | `Δ < -300` | 🔴 Đỏ |

---

## 🔌 API Endpoints

| Endpoint | Method | Mô tả |
|---|---|---|
| `/` | GET | Health check root |
| `/api/analyze` | POST | Gửi FEN để phân tích |
| `/api/analyze/status` | GET | Trạng thái engine + request stats |
| `/api/analyze/diagnostics` | GET | Deep diagnostics: PID, uptime, restart count |
| `/api/analyze/latest` | GET | Kết quả phân tích gần nhất |
| `/api/analyze/reset` | POST | Reset state khi bắt đầu ván mới |
| `/chessHub` | WebSocket | SignalR hub — stream kết quả realtime |

---

## 📁 Cấu trúc dự án

```
ChessAssistantRoot/
├── brain-backend/          # 🧠 C# ASP.NET Core 9 — Stockfish wrapper
│   ├── Controllers/        #    AnalysisController (POST /api/analyze)
│   ├── Hubs/               #    SignalR ChessHub
│   ├── Logging/            #    ChessConsoleFormatter (colored logs)
│   ├── Services/           #    StockfishService, FenCacheService, EvaluationService
│   ├── Models/             #    AnalysisRequest, AnalysisResult, MoveInfo
│   ├── Engine/             #    Stockfish binary (gitignored)
│   └── appsettings.json
│
├── chrome-extension/       # 🔌 Chrome Extension (Manifest V3)
│   ├── content.js          #    Đọc DOM bàn cờ → gửi FEN
│   ├── popup.html/js       #    Giao diện popup
│   └── manifest.json
│
├── overlay-ui/             # 🎨 React 19 + Vite — Transparent Overlay
│   └── src/
│       ├── components/     #    Arrows, EvalBar, InfoPanel
│       └── hooks/          #    useSignalR, useCoordinateMap
│
├── vision-module/          # 👁️ Python — Computer Vision (optional)
│   ├── detector.py         #    Board detection (OpenCV)
│   ├── fen_converter.py    #    Piece recognition
│   └── requirements.txt
│
├── setup_linux.sh          # 🛠️ Auto-setup: Ubuntu/Debian/Arch/Fedora...
├── setup_windows.ps1       # 🛠️ Auto-setup: Windows 10/11
├── START_HERE.sh           # 🚀 Quick start (Linux)
└── START_HERE.bat          # 🚀 Quick start (Windows)
```

---

## 📦 Tech Stack

| Module | Công nghệ |
|---|---|
| Brain Backend | C# / ASP.NET Core 9 / SignalR / Stockfish 17 |
| Chrome Extension | JavaScript / Manifest V3 / Chrome APIs |
| Overlay UI | React 19 / Tailwind CSS 4 / Vite |
| Vision Module | Python 3.11 / OpenCV / ONNX Runtime |

---

## 📄 License

MIT License — xem file [LICENSE](LICENSE).

---

<p align="center">
  Made with ♟️ by <a href="https://github.com/Nhanduc2912">Nhanduc2912</a>
</p>
