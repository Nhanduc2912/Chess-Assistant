import time
import requests

# FEN sequence for a standard opening (e.g., Italian Game)
MOCK_FENS = [
    "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", # Start
    "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1", # 1. e4
    "rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2", # 1... e5
    "rnbqkbnr/pppp1ppp/8/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 1 2", # 2. Nf3
    "r1bqkbnr/pppp1ppp/2n5/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 2 3", # 2... Nc6
    "r1bqkbnr/pppp1ppp/2n5/1B2p3/4P3/5N2/PPPP1PPP/RNBQK2R b KQkq - 3 3", # 3. Bb5 (Ruy Lopez)
    "r1bqkbnr/1ppp1ppp/p1n5/1B2p3/4P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 0 4", # 3... a6
    "r1bqkbnr/1ppp1ppp/p1n5/4p3/B3P3/5N2/PPPP1PPP/RNBQK2R b KQkq - 1 4", # 4. Ba4
    "r1bqkb1r/1ppp1ppp/p1n2n2/4p3/B3P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 2 5", # 4... Nf6
    "r1bqkb1r/1ppp1ppp/p1n2n2/4p3/B3P3/5N2/PPPP1PPP/RNBQ1RK1 b kq - 3 5" # 5. O-O
]

# Simulate a bounding box that takes up a central part of a 1920x1080 screen
MOCK_BBOX = {
    "x": 560,
    "y": 140,
    "w": 800,
    "h": 800
}

BACKEND_URL = "http://localhost:5000/api/analyze"

def main():
    print("===================================================")
    print(" CHESS REALTIME ASSISTANT - MOCK VISION MODULE")
    print("===================================================")
    print("This script simulates the vision module by sending")
    print("a predefined sequence of FENs to the backend.")
    print("Use this to test the Overlay UI without needing a")
    print("real chessboard on screen.")
    print("===================================================\n")
    
    idx = 0
    while True:
        fen = MOCK_FENS[idx]
        print(f"[{idx+1}/{len(MOCK_FENS)}] Sending FEN: {fen}")
        
        payload = {
            "fen": fen,
            "bbox": MOCK_BBOX
        }
        
        try:
            resp = requests.post(BACKEND_URL, json=payload, timeout=2)
            if resp.status_code == 200:
                print("  -> Backend processing...")
            else:
                print(f"  -> ERROR: Backend returned {resp.status_code}")
        except Exception as e:
            print(f"  -> ERROR: Could not connect to backend ({e})")
            
        idx = (idx + 1) % len(MOCK_FENS)
        
        # Wait 5 seconds before next move to give Stockfish time
        # to analyze and UI time to render
        time.sleep(5)

if __name__ == "__main__":
    main()
