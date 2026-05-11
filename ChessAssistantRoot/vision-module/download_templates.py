import os
import requests

THEME = "neo"
SIZE = 150
BASE_URL = f"https://images.chesscomfiles.com/chess-themes/pieces/{THEME}/{SIZE}/"

PIECES = {
    'P': 'wp.png', 'N': 'wn.png', 'B': 'wb.png', 'R': 'wr.png', 'Q': 'wq.png', 'K': 'wk.png',
    'p': 'bp.png', 'n': 'bn.png', 'b': 'bb.png', 'r': 'br.png', 'q': 'bq.png', 'k': 'bk.png'
}

OUT_DIR = "templates/chesscom_default"

def download_templates():
    if not os.path.exists(OUT_DIR):
        os.makedirs(OUT_DIR)
        
    for fen_char, filename in PIECES.items():
        url = BASE_URL + filename
        
        # Windows case insensitivity workaround
        color = "white" if fen_char.isupper() else "black"
        out_name = f"{fen_char}_{color}.png"
        out_path = os.path.join(OUT_DIR, out_name)
        
        if os.path.exists(out_path):
            print(f"[OK] {out_path} already exists.")
            continue
            
        print(f"Downloading {url} to {out_path}...")
        try:
            resp = requests.get(url, timeout=5)
            if resp.status_code == 200:
                with open(out_path, 'wb') as f:
                    f.write(resp.content)
            else:
                print(f"[ERROR] Failed to download {filename}")
        except Exception as e:
            print(f"[ERROR] {e}")

if __name__ == "__main__":
    download_templates()
