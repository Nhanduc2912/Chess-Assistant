# ♟️ Chess Realtime Assistant

> Hệ thống trợ lý cờ vua thời gian thực — phân tích bàn cờ trực tiếp từ Chess.com & Lichess bằng Stockfish engine.

![Version](https://img.shields.io/badge/version-2.0-blue)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)
![License](https://img.shields.io/badge/license-MIT-green)

---

## 🎯 Tính năng

- ⚡ **Phân tích thời gian thực** — Tự động phát hiện nước đi và đưa ra gợi ý tốt nhất
- 🧠 **Stockfish 16+ Engine** — MultiPV 3 đường, Depth 20, cache thông minh
- 🔌 **Chrome Extension** — Tích hợp trực tiếp vào Chess.com & Lichess, không cần phần mềm ngoài
- 👁️ **Vision Module** — Nhận diện bàn cờ qua Computer Vision (CNN + Template Matching)
- 🎨 **Overlay UI** — Mũi tên SVG hiển thị trực tiếp trên bàn cờ, phân loại nước đi theo màu sắc
- 📊 **Đánh giá chi tiết** — Brilliant, Best, Good, Inaccuracy, Mistake, Blunder

---

## 🏗️ Kiến trúc hệ thống

```
┌─────────────────────────────────────────────────────────┐
│                    TRÌNH DUYỆT                          │
│                                                         │
│  ┌──────────────┐          ┌─────────────────────────┐ │
│  │  Chess.com /  │          │  Chrome Extension        │ │
│  │  Lichess      │◄─ DOM ──│  (content.js + popup)    │ │
│  └──────────────┘          └──────────┬──────────────┘ │
└───────────────────────────────────────┼─────────────────┘
                                        │ HTTP POST
                                        ▼
┌───────────────────────────────────────────────────────────┐
│                    MÁY LOCAL                              │
│                                                           │
│  ┌──────────────────┐          ┌───────────────────────┐ │
│  │  Vision Module    │  HTTP   │   Brain Backend        │ │
│  │  (Python)         │────────▶│   (ASP.NET Core 9)    │ │
│  │                   │         │                        │ │
│  │  • Screenshot     │         │  • Stockfish Engine    │ │
│  │  • Board Detect   │         │  • Eval & Classify     │ │
│  │  • FEN Convert    │         │  • SignalR Hub          │ │
│  └──────────────────┘         └───────────────────────┘ │
└───────────────────────────────────────────────────────────┘
```

---

## 📦 Cấu trúc thư mục

```
Chess-Assistant/
├── ChessAssistantRoot/
│   ├── brain-backend/          # 🧠 C# ASP.NET Core 9 — Stockfish wrapper
│   │   ├── Controllers/        #    POST /api/analysis
│   │   ├── Hubs/               #    SignalR WebSocket hub
│   │   ├── Services/           #    Stockfish, Evaluation, Cache
│   │   ├── Models/             #    Request/Response DTOs
│   │   ├── Engine/             #    Stockfish binary (download riêng)
│   │   └── Program.cs
│   │
│   ├── chrome-extension/       # 🔌 Chrome Extension (Manifest V3)
│   │   ├── content.js          #    Đọc DOM bàn cờ → gửi FEN
│   │   ├── popup.html/js       #    Giao diện popup điều khiển
│   │   ├── background.js       #    Service worker
│   │   └── manifest.json
│   │
│   ├── overlay-ui/             # 🎨 React + Tailwind — Transparent Overlay
│   │   └── src/
│   │       ├── components/     #    Arrows, EvalBar, InfoPanel, StatusBadge
│   │       ├── hooks/          #    useSignalR, useCoordinateMap
│   │       └── utils/          #    Chess coordinate mapping
│   │
│   ├── vision-module/          # 👁️ Python — Computer Vision (optional)
│   │   ├── detector.py         #    Board detection via OpenCV
│   │   ├── fen_converter.py    #    Piece recognition (CNN/ONNX)
│   │   ├── main.py             #    Main loop + hash diff
│   │   └── requirements.txt
│   │
│   ├── START_HERE.bat           # 🚀 Quick start script
│   ├── start_assistant.bat
│   └── stop_assistant.bat
│
├── .gitignore
└── README.md
```

---

## 🚀 Cài đặt & Chạy

### Yêu cầu hệ thống

| Thành phần   | Yêu cầu                          |
|-------------|-----------------------------------|
| OS          | Windows 10/11 (x64)              |
| Runtime     | [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) |
| Browser     | Google Chrome (cho Extension)     |
| Python      | 3.11+ (chỉ nếu dùng Vision Module) |

### Bước 1: Clone repo

```bash
git clone https://github.com/Nhanduc2912/Chess-Assistant.git
cd Chess-Assistant
```

### Bước 2: Tải Stockfish Engine

```powershell
cd ChessAssistantRoot/brain-backend
powershell -ExecutionPolicy Bypass -File download_stockfish.ps1
```

Hoặc tải thủ công từ [Stockfish Releases](https://github.com/official-stockfish/Stockfish/releases) và đặt `stockfish.exe` vào thư mục `brain-backend/Engine/`.

### Bước 3: Chạy Brain Backend

```bash
cd ChessAssistantRoot/brain-backend
dotnet restore
dotnet run
```

Backend sẽ khởi chạy tại `http://localhost:5000`.

### Bước 4: Cài Chrome Extension

1. Mở Chrome → nhập `chrome://extensions/`
2. Bật **Developer mode** (góc phải trên)
3. Click **Load unpacked** → chọn thư mục `ChessAssistantRoot/chrome-extension/`
4. Extension "Chess Realtime Assistant" sẽ xuất hiện trên thanh toolbar

### Bước 5: Sử dụng

1. Mở [Chess.com](https://www.chess.com) hoặc [Lichess](https://lichess.org)
2. Bắt đầu một ván cờ
3. Click icon extension trên toolbar Chrome
4. Trạng thái hiển thị **"Stockfish Online"** → hệ thống sẵn sàng!
5. Mỗi nước đi sẽ tự động được phân tích

> 💡 **Mẹo:** Nếu popup hiển thị "Content script not loaded", nhấn **F5** để refresh trang chess.

---

## ⚙️ Cấu hình

### Brain Backend (`appsettings.json`)

```json
{
  "Stockfish": {
    "MultiPV": 3,
    "Depth": 20,
    "Threads": 2,
    "Hash": 128
  }
}
```

### Vision Module (`config.yaml`)

```yaml
vision:
  fps_target: 10
  cnn_confidence_threshold: 0.85
backend:
  url: "http://localhost:5000/api/analyze"
```

---

## 🎨 Phân loại nước đi

| Delta (Centipawns)    | Phân loại     | Màu sắc       |
|----------------------|---------------|----------------|
| `< -300`             | 💀 Blunder    | 🔴 Đỏ         |
| `-300 ≤ Δ < -150`    | ❌ Mistake    | 🟠 Cam         |
| `-150 ≤ Δ < -50`     | ⚠️ Inaccuracy | 🟡 Vàng        |
| `-50 ≤ Δ < 0`        | ✅ Good       | 🟢 Xanh lá nhạt|
| `Δ = 0`              | ⭐ Best       | 🟢 Xanh lá đậm|
| `Δ > 0`              | 💡 Brilliant  | 🟣 Tím         |

---

## 🔌 API Endpoints

| Endpoint          | Method    | Mô tả                          |
|-------------------|-----------|---------------------------------|
| `/`               | GET       | Health check                    |
| `/api/analysis`   | POST      | Gửi FEN để phân tích            |
| `/api/analysis/status` | GET  | Trạng thái Stockfish engine     |
| `/chessHub`       | WebSocket | SignalR hub — nhận kết quả realtime |

---

## 🛠️ Phát triển

### Overlay UI (Optional — Electron overlay)

```bash
cd ChessAssistantRoot/overlay-ui
npm install
npm run dev
```

### Vision Module (Optional — Computer Vision)

```bash
cd ChessAssistantRoot/vision-module
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt
python main.py
```

---

## 📁 Tech Stack

| Module           | Công nghệ                                    |
|-----------------|-----------------------------------------------|
| Brain Backend   | C# / ASP.NET Core 9 / SignalR / Stockfish 16 |
| Chrome Extension| JavaScript / Manifest V3 / Chrome APIs        |
| Overlay UI      | React 19 / Tailwind CSS 4 / Electron / Vite   |
| Vision Module   | Python 3.11 / OpenCV / ONNX Runtime / NumPy   |

---

## 📄 License

MIT License — xem file [LICENSE](LICENSE) để biết thêm chi tiết.

---

<p align="center">
  Made with ♟️ by <a href="https://github.com/Nhanduc2912">Nhanduc2912</a>
</p>
