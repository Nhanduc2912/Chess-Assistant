# Chrome Extension — Cài đặt và sử dụng

## Mục đích
Extension này thay thế hoàn toàn module Vision Python.  
Thay vì chụp màn hình (không đáng tin cậy), extension **đọc trực tiếp vị trí quân cờ từ DOM của Chess.com và Lichess**, đảm bảo độ chính xác 100%.

## Cách cài đặt (Load Unpacked)

1. Mở Chrome / Edge
2. Truy cập `chrome://extensions/` (hoặc `edge://extensions/`)
3. Bật **Developer mode** (góc trên bên phải)
4. Nhấn **"Load unpacked"**
5. Trỏ vào thư mục: `D:\WEB\Chess-assistant\ChessAssistantRoot\chrome-extension`
6. Extension xuất hiện trên toolbar ✅

## Cách dùng

1. Chạy file **`START_HERE.bat`** (khởi động Backend C# + UI React)
2. Mở **Chess.com** hoặc **Lichess**, vào 1 ván cờ
3. Click icon **♟ Chess Assistant** trên toolbar Chrome
4. Popup hiện ra với:
   - 🟢 **Engine Online** — Stockfish đang chạy
   - Điểm đánh giá (+/-) theo centipawns
   - Top 3 nước đi tốt nhất
   - Phân loại nước đi (Best/Good/Blunder...)
5. Mỗi khi bạn đi 1 nước, extension tự động gửi FEN mới về backend và cập nhật popup trong vòng <1 giây

## Hoạt động thế nào

```
Chess.com DOM  →  content.js  →  POST /api/analyze  →  Stockfish
                                                           ↓
Extension Popup  ←  GET /api/analyze/latest  ←  AnalysisResult
```

## Hỗ trợ
- Chess.com: ✅ (piece class: `wp`, `bn`, etc. + `square-e2`)
- Lichess: ✅ (piece class: `white pawn`, transform position)
