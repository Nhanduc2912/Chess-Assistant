# [AGENT_PLAN] CHESS-REALTIME-ASSISTANT v2.0

> **Trạng thái:** Production-Ready Blueprint  
> **Phiên bản:** 2.0 — Revised & Hardened  
> **Mục tiêu:** Hệ thống phân tích cờ vua thời gian thực qua Computer Vision + Stockfish Engine, hoàn toàn local, không can thiệp DOM/Web.

---

## 0. ĐỌC TRƯỚC KHI LÀM (AGENT MUST READ)

> ⚠️ **Agent phải đọc toàn bộ section này trước khi viết bất kỳ dòng code nào.**

### Những vấn đề thực tế của v1 (đã được fix trong v2)

| Vấn đề v1                                  | Hệ quả                                        | Fix trong v2                                     |
| ------------------------------------------ | --------------------------------------------- | ------------------------------------------------ |
| FEN Converter dùng template matching thuần | Fail khi đổi theme bàn cờ, lighting, scale    | Dùng CNN nhẹ (ONNX) + template matching fallback |
| Vision → Backend dùng HTTP POST mỗi frame  | 10fps = 10 request/s, bottleneck nghiêm trọng | Chỉ POST khi FEN **thay đổi** (hash diff)        |
| Không có coordinate mapping                | Arrow SVG không biết vẽ ở đâu trên màn hình   | Vision phải export `board_bbox` cùng FEN         |
| MultiPV = 5 cố định                        | Tăng latency Stockfish không cần thiết        | Dynamic MultiPV (default 3, configurable)        |
| Không có dedup/cache                       | Stockfish phân tích lại cùng 1 FEN liên tục   | Cache kết quả theo FEN hash                      |

### Nguyên tắc bất biến (Agent không được vi phạm)

1. **Không overwrite** file khi chưa đọc logic cũ — dùng Read/Search trước.
2. **Agent config** chỉ nằm trong `.agent_config/` — không làm bẩn source code.
3. **Ba module** (Vision, Brain, UI) phải hoàn toàn độc lập — giao tiếp qua HTTP REST & SignalR.
4. **Log tất cả lỗi runtime** vào `.agent_config/system.log`.
5. **Mỗi giai đoạn** phải test xong trước khi sang giai đoạn tiếp theo.

---

## 1. KIẾN TRÚC TỔNG THỂ (v2 — Revised)

```
┌─────────────────────────────────────────────────────────────────┐
│                        USER'S SCREEN                            │
│                                                                 │
│   ┌─────────────────┐          ┌──────────────────────────┐    │
│   │   Chess Website  │          │    React Overlay UI       │    │
│   │  (Chess.com,     │          │  (pointer-events: none)   │    │
│   │   Lichess, etc.) │          │  SVG Arrows + Evaluation  │    │
│   └─────────────────┘          └──────────┬───────────────┘    │
│                                            │ SignalR WS         │
└────────────────────────────────────────────┼───────────────────┘
                                             │
     ┌───────────────────────────────────────▼──────────────────┐
     │                    LOCAL MACHINE                          │
     │                                                           │
     │  ┌──────────────┐   HTTP POST    ┌────────────────────┐  │
     │  │ Vision Module │  (FEN+BBox)   │   Brain Backend    │  │
     │  │   (Python)    │──────────────▶│  (ASP.NET Core 9)  │  │
     │  │               │               │                    │  │
     │  │ • Screenshot  │               │ • Stockfish Engine │  │
     │  │ • Board Detect│               │ • Eval & Classify  │  │
     │  │ • CNN + FEN   │               │ • SignalR Hub       │  │
     │  │ • Hash Diff   │               │ • FEN Cache        │  │
     │  └──────────────┘               └────────────────────┘  │
     └───────────────────────────────────────────────────────────┘
```

### Data Flow chi tiết (v2)

```
[Màn hình]
    │ mss chụp vùng ROI (30fps)
    ▼
[Vision: detector.py]
    │ OpenCV → Warp perspective → Chuẩn hóa 512x512
    ▼
[Vision: fen_converter.py]
    │ ONNX CNN classify từng ô (64 ô × 13 class)
    │ Fallback: Template matching nếu CNN confidence < 0.85
    ▼
[Vision: main.py — Hash Diff]
    │ SHA256(FEN) == SHA256(FEN trước) → BỎ QUA
    │ SHA256(FEN) khác → POST {"fen": "...", "bbox": {...}}
    ▼
[Brain: AnalysisController]
    │ Kiểm tra FEN Cache (Redis-like in-memory)
    │ Cache hit → Dùng kết quả cũ
    │ Cache miss → Gọi Stockfish
    ▼
[Brain: StockfishService]
    │ MultiPV = 3 (configurable)
    │ Depth = 20 (configurable)
    │ Tính Delta = score_hiện_tại - score_trước
    ▼
[Brain: EvaluationService]
    │ Phân loại nước đi (xem bảng phân loại bên dưới)
    ▼
[Brain: ChessHub — SignalR]
    │ Emit "ReceiveAnalysis" → tất cả client
    ▼
[UI: useSignalR.js]
    │ Nhận data → Map algebraic notation → Pixel coordinates
    │ (dùng board_bbox từ Vision để tính tọa độ)
    ▼
[UI: Arrows.jsx]
    SVG arrows render on top of chess board
```

---

## 2. CẤU TRÚC THƯ MỤC (v2)

```
/ChessAssistantRoot
├── .agent_config/
│   ├── system.log               # Runtime logs của Agent
│   ├── progress.json            # Tracking tiến độ các giai đoạn
│   └── config.json              # Agent metadata
│
├── /vision-module/              # Python 3.11+ (Con mắt)
│   ├── main.py                  # Vòng lặp chính + hash diff logic
│   ├── detector.py              # OpenCV: chụp, detect, warp board
│   ├── fen_converter.py         # CNN (ONNX) + template matching fallback
│   ├── models/
│   │   └── chess_piece_cnn.onnx # Pre-trained model (download script kèm theo)
│   ├── templates/               # Template images fallback (per theme)
│   │   ├── lichess_default/
│   │   └── chesscom_default/
│   ├── utils/
│   │   ├── hash_utils.py        # SHA256 FEN diff
│   │   └── bbox_utils.py        # Board bounding box calculator
│   ├── config.yaml              # ROI config, threshold, target URL
│   └── requirements.txt
│
├── /brain-backend/              # C# ASP.NET Core 9 (Bộ não)
│   ├── Controllers/
│   │   └── AnalysisController.cs    # POST /api/analyze
│   ├── Hubs/
│   │   └── ChessHub.cs              # SignalR: emit ReceiveAnalysis
│   ├── Services/
│   │   ├── StockfishService.cs      # Wrapper Stockfish process
│   │   ├── EvaluationService.cs     # Phân loại nước đi
│   │   └── FenCacheService.cs       # In-memory cache theo FEN hash
│   ├── Models/
│   │   ├── AnalysisRequest.cs       # { fen, bbox }
│   │   └── AnalysisResult.cs        # { bestMoves[], evaluation, classification }
│   ├── Engine/
│   │   └── stockfish.exe            # Stockfish 16+ binary
│   ├── appsettings.json
│   └── Program.cs
│
└── /overlay-ui/                 # React 18 + Tailwind (Giao diện)
    ├── src/
    │   ├── components/
    │   │   ├── Arrows.jsx           # SVG arrows với coordinate mapping
    │   │   ├── EvalBar.jsx          # Thanh đánh giá centipawns
    │   │   └── StatusBadge.jsx      # "Engine Online/Offline"
    │   ├── hooks/
    │   │   ├── useSignalR.js        # WebSocket connection + reconnect
    │   │   └── useCoordinateMap.js  # Algebraic → Pixel converter
    │   ├── utils/
    │   │   └── chessCoords.js       # e2→e4 to (x1,y1)→(x2,y2)
    │   └── App.jsx
    ├── tailwind.config.js
    └── package.json
```

---

## 3. THÔNG SỐ KỸ THUẬT CHI TIẾT

### 3.1 Vision Module

#### detector.py — Board Detection Pipeline

```python
# Thuật toán detect bàn cờ:
# 1. Chụp toàn màn hình với mss (tốc độ cao hơn PIL)
# 2. Convert sang grayscale → Canny edge detection
# 3. Tìm contour hình chữ nhật lớn nhất (board candidate)
# 4. Perspective warp về 512x512 chuẩn
# 5. Export: warped_image + board_bbox (x, y, w, h trên màn hình gốc)

# Fail condition: Không tìm thấy contour hợp lệ
# → Raise BoardNotFoundError → main.py dừng gửi request
```

#### fen_converter.py — Piece Recognition

```python
# Pipeline:
# 1. Chia warped 512x512 thành 64 ô (8x8), mỗi ô 64x64px
# 2. Với mỗi ô:
#    a. ONNX CNN inference (13 class: 6 trắng + 6 đen + empty)
#    b. Nếu confidence < 0.85 → fallback template matching
# 3. Ghép 64 ký tự thành FEN string
# 4. Validate FEN bằng python-chess trước khi export

# Classes: P,N,B,R,Q,K (trắng), p,n,b,r,q,k (đen), . (empty)
# Model: chess_piece_cnn.onnx (~15MB, inference ~5ms/frame)
```

#### main.py — Hash Diff & Rate Control

```python
# Vòng lặp chính:
# 1. Chụp màn hình mỗi 100ms (10fps target)
# 2. Gọi detector → fen_converter
# 3. SHA256(new_fen) vs SHA256(last_sent_fen)
# 4. Nếu khác → POST {"fen": new_fen, "bbox": board_bbox}
# 5. Nếu giống → Skip (không gửi)
# 6. BoardNotFoundError → Log + skip (không crash)
```

#### config.yaml

```yaml
vision:
  fps_target: 10
  cnn_confidence_threshold: 0.85
  board_detection_min_area: 40000 # pixels^2

backend:
  url: "http://localhost:5000/api/analyze"
  timeout_seconds: 2

templates:
  theme: "chesscom_default" # hoặc "lichess_default"
```

---

### 3.2 Brain Backend (ASP.NET Core 9)

#### StockfishService.cs

```csharp
// Config mặc định:
// - MultiPV: 3 (configurable qua appsettings)
// - Depth: 20
// - Threads: 2 (không chiếm hết CPU)
// - Hash: 128MB

// Process management:
// - Spawn 1 Stockfish process khi app start
// - Giao tiếp qua stdin/stdout
// - Timeout mỗi analysis: 500ms
// - Nếu process crash → tự restart
```

#### EvaluationService.cs — Bảng phân loại nước đi

| Delta (Centipawns)    | Phân loại     | Màu arrow    |
| --------------------- | ------------- | ------------ |
| `< -300`              | 💀 Blunder    | Đỏ           |
| `-300 ≤ delta < -150` | ❌ Mistake    | Cam          |
| `-150 ≤ delta < -50`  | ⚠️ Inaccuracy | Vàng         |
| `-50 ≤ delta < 0`     | ✅ Good Move  | Xanh lá nhạt |
| `delta = 0`           | ⭐ Best Move  | Xanh lá đậm  |
| `delta > 0`           | 💡 Brilliant  | Tím          |

```csharp
// Delta = score_sau_nuoc_vua - score_truoc_nuoc_vua
// Score tính theo góc nhìn người chơi hiện tại (luôn dương = tốt cho họ)
```

#### FenCacheService.cs

```csharp
// In-memory dictionary: Dictionary<string, AnalysisResult>
// Key: SHA256(FEN)
// Max size: 500 entries (LRU eviction)
// TTL: 5 phút
// Mục đích: Tránh analyze lại cùng position khi người dùng không đi
```

#### AnalysisController.cs

```csharp
// POST /api/analyze
// Body: { "fen": "...", "bbox": { "x": 0, "y": 0, "w": 800, "h": 800 } }
// Response: 200 OK (async, kết quả emit qua SignalR)
// Error: 400 nếu FEN không hợp lệ, 503 nếu Stockfish down
```

#### ChessHub.cs (SignalR)

```csharp
// Hub URL: /chessHub
// Event: "ReceiveAnalysis"
// Payload:
// {
//   "bestMoves": [
//     { "move": "e2e4", "score": 45, "depth": 20 },
//     { "move": "d2d4", "score": 40, "depth": 20 },
//     { "move": "g1f3", "score": 35, "depth": 20 }
//   ],
//   "evaluation": 45,           // centipawns
//   "classification": "Good",   // Blunder/Mistake/Inaccuracy/Good/Best/Brilliant
//   "bbox": { "x":0,"y":0,"w":800,"h":800 }
// }
```

#### Program.cs — Startup Config

```csharp
// Services cần đăng ký:
// - AddSignalR()
// - AddSingleton<StockfishService>()
// - AddSingleton<FenCacheService>()
// - AddScoped<EvaluationService>()
// CORS: Cho phép localhost:3000 (React dev) và file:// (Electron nếu cần)
// Middleware: UseRouting → UseCors → UseEndpoints
```

---

### 3.3 Overlay UI (React 18)

#### App.jsx — Transparent Overlay

```jsx
// Style bắt buộc:
// position: fixed, top: 0, left: 0
// width: 100vw, height: 100vh
// pointer-events: none        ← click xuyên qua
// z-index: 9999
// background: transparent
// overflow: hidden
```

#### useCoordinateMap.js — Tọa độ quan trọng nhất

```javascript
// Input: algebraic move ("e2e4"), board_bbox ({x,y,w,h})
// Output: { x1, y1, x2, y2 } — pixel coordinates trên màn hình

// Cách tính:
// board_bbox cho biết bàn cờ nằm ở đâu trên màn hình (từ Vision module)
// Chia bbox thành 8x8 grid
// "e2" → col=4, row=6 (tính từ góc trên trái, tùy orientation)
// x = bbox.x + col * (bbox.w / 8) + (bbox.w / 16)  ← center của ô
// y = bbox.y + row * (bbox.h / 8) + (bbox.h / 16)
```

#### Arrows.jsx

```jsx
// SVG arrow từ (x1,y1) đến (x2,y2)
// Màu arrow theo classification (xem bảng EvaluationService)
// Opacity: top 1 move = 1.0, move 2 = 0.6, move 3 = 0.3
// Animation: fade in 150ms
// Arrowhead: SVG marker element
```

#### useSignalR.js

```javascript
// Kết nối: "http://localhost:5000/chessHub"
// Reconnect: exponential backoff (1s, 2s, 4s, 8s, max 30s)
// Khi mất kết nối: set state "offline" → StatusBadge hiển thị "Engine Offline"
// Khi reconnect: set state "online" → StatusBadge ẩn đi
```

---

## 4. KẾ HOẠCH TRIỂN KHAI (Implementation Plan)

### Giai đoạn 1: Brain Backend (Ưu tiên cao nhất)

> **Lý do:** UI và Vision đều phụ thuộc vào Backend. Phải xây trụ cột trước.

**Tasks theo thứ tự:**

1. Khởi tạo ASP.NET Core 9 project với cấu trúc đúng
2. Tích hợp Stockfish (spawn process, stdin/stdout comm)
3. Implement `StockfishService` với config MultiPV=3, Depth=20
4. Implement `FenCacheService` (in-memory, LRU 500 entries)
5. Implement `EvaluationService` (bảng phân loại)
6. Implement `AnalysisController` (POST /api/analyze)
7. Implement `ChessHub` (SignalR, emit ReceiveAnalysis)
8. **Test:** Dùng Postman POST FEN hợp lệ → Verify nhận được analysis qua SignalR

**Definition of Done:** Gửi FEN bằng Postman → SignalR client nhận được `ReceiveAnalysis` với đủ trường trong < 1 giây.

---

### Giai đoạn 2: Vision Module

> **Chú ý:** FEN Converter là phần khó nhất, không được rush.

**Tasks theo thứ tự:**

1. Cài đặt môi trường Python 3.11, `requirements.txt`
2. Implement `detector.py`: chụp màn hình → detect board → warp 512x512
3. **Test detector trước** với ảnh chụp tay Chess.com và Lichess
4. Download/tích hợp `chess_piece_cnn.onnx` (xem note bên dưới)
5. Implement `fen_converter.py`: CNN inference + template matching fallback
6. Implement `hash_utils.py` (SHA256 diff)
7. Implement `bbox_utils.py` (export board bbox)
8. Implement `main.py`: vòng lặp chính + error handling
9. **Test end-to-end:** Mở Chess.com → Run vision → Verify FEN gửi đúng đến Backend

**Note về CNN model:**

- Option A: Dùng pre-trained model từ [chess-vision](https://github.com/Georg-code/chess-recognition) (MIT license)
- Option B: Nếu không có internet, implement template matching cho cả 2 theme (Chess.com dark + Lichess brown)
- Agent phải kiểm tra file `models/chess_piece_cnn.onnx` trước khi code CNN path

**Definition of Done:** Vision module detect đúng FEN > 95% các position test, chỉ gửi POST khi FEN thay đổi.

---

### Giai đoạn 3: Overlay UI

**Tasks theo thứ tự:**

1. Setup React 18 + Tailwind + SignalR client (`@microsoft/signalr`)
2. Implement `useSignalR.js` với reconnect logic
3. Implement `useCoordinateMap.js` (quan trọng nhất, test kỹ)
4. Implement `Arrows.jsx` với SVG arrows + màu sắc theo classification
5. Implement `EvalBar.jsx` (thanh +/- centipawns)
6. Implement `StatusBadge.jsx`
7. Assemble trong `App.jsx` với transparent overlay style
8. **Test:** Chạy cả 3 module → Di chuyển quân cờ → Verify arrow xuất hiện đúng vị trí

**Definition of Done:** Arrow render đúng ô, đúng màu, đúng position trên màn hình trong < 200ms sau khi đi quân.

---

### Giai đoạn 4: Tối ưu & Integration Test

1. Đo end-to-end latency: Đi quân → Arrow xuất hiện (target: < 500ms)
2. Test với nhiều theme bàn cờ (Chess.com light/dark, Lichess)
3. Test edge cases: Promotion, Castling (FEN đặc biệt)
4. Stress test: để chạy 30 phút liên tục
5. Tối ưu nếu latency > 500ms (tăng Stockfish threads, giảm depth, hoặc tối ưu CNN)

---

## 5. ERROR HANDLING & FAIL-SAFE

| Tình huống                       | Behavior                        | Log                                             |
| -------------------------------- | ------------------------------- | ----------------------------------------------- |
| Vision: Không detect được bàn cờ | Skip frame, không gửi POST      | `[WARN] Board not detected in frame {n}`        |
| Vision: CNN confidence thấp      | Dùng template matching fallback | `[INFO] Falling back to template matching`      |
| Vision: FEN không hợp lệ         | Discard, không gửi              | `[WARN] Invalid FEN generated: {fen}`           |
| Backend: Stockfish không respond | Return 503, retry sau 1s        | `[ERROR] Stockfish timeout on FEN: {fen}`       |
| Backend: Stockfish process crash | Auto-restart, log event         | `[CRITICAL] Stockfish process died, restarting` |
| UI: Mất kết nối SignalR          | Hiển thị "Engine Offline" badge | —                                               |
| UI: bbox không hợp lệ            | Ẩn arrows, không crash          | `[WARN] Invalid bbox received`                  |

**Log format:** `[YYYY-MM-DD HH:MM:SS] [LEVEL] [MODULE] Message`  
**Log location:** `.agent_config/system.log`

---

## 6. DEPENDENCIES

### Vision Module (requirements.txt)

```
mss==9.0.1
opencv-python==4.9.0.80
onnxruntime==1.17.1
python-chess==1.999
requests==2.31.0
pyyaml==6.0.1
numpy==1.26.4
```

### Brain Backend (NuGet)

```
Stockfish (NuGet package hoặc process wrapper tự viết)
Microsoft.AspNetCore.SignalR.Core
Newtonsoft.Json (hoặc System.Text.Json)
```

### Overlay UI (package.json)

```json
{
  "dependencies": {
    "react": "^18.2.0",
    "@microsoft/signalr": "^8.0.0",
    "tailwindcss": "^3.4.0"
  }
}
```

---

## 7. PORTS & ENDPOINTS REFERENCE

| Service          | Port | Endpoint       | Method    |
| ---------------- | ---- | -------------- | --------- |
| Brain Backend    | 5000 | `/api/analyze` | POST      |
| Brain Backend    | 5000 | `/chessHub`    | WebSocket |
| Overlay UI (dev) | 3000 | —              | —         |

**CORS Backend phải allow:** `http://localhost:3000`

---

## 8. CHECKLIST TRƯỚC KHI BÀN GIAO

- [ ] Giai đoạn 1 Done: Postman test pass
- [ ] Giai đoạn 2 Done: Vision test với 10 position mẫu, accuracy > 95%
- [ ] Giai đoạn 3 Done: Arrow render đúng với coordinate mapping
- [ ] Giai đoạn 4 Done: Latency < 500ms, stress test 30 phút OK
- [ ] Log file hoạt động và có nội dung
- [ ] Không có file nào của Agent ngoài `.agent_config/`
- [ ] `config.yaml` và `appsettings.json` có thể chỉnh mà không cần rebuild

---

## 9. GHI CHÚ QUAN TRỌNG CHO AGENT

> **Đọc kỹ trước khi code bất kỳ thứ gì.**

1. **Bắt đầu từ Giai đoạn 1** (Brain Backend) — không được nhảy sang Vision hay UI trước.
2. **Coordinate mapping** là điểm fail phổ biến nhất — test riêng `useCoordinateMap.js` trước khi tích hợp.
3. **FEN validation** bằng `python-chess` là bắt buộc — một FEN sai sẽ làm Stockfish crash.
4. **Không hardcode port hay URL** — để trong `config.yaml` và `appsettings.json`.
5. **Stockfish process** phải được dispose đúng cách khi app shutdown để tránh zombie process.
6. **CNN model** là optional dependency — code phải fallback về template matching nếu file `.onnx` không tồn tại.
7. Mọi thay đổi phải được log vào `.agent_config/progress.json` theo format:

```json
{
  "phase": 1,
  "status": "in_progress",
  "completed_tasks": ["StockfishService", "FenCacheService"],
  "next_task": "EvaluationService",
  "last_updated": "2025-01-01T00:00:00Z"
}
```
