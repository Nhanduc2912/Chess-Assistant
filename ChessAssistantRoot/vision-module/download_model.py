import urllib.request
import os

MODEL_URL = "https://raw.githubusercontent.com/Georg-code/chess-recognition/master/models/chess_piece_cnn.onnx"
MODEL_DIR = "models"
MODEL_PATH = os.path.join(MODEL_DIR, "chess_piece_cnn.onnx")

def download_model():
    if not os.path.exists(MODEL_DIR):
        os.makedirs(MODEL_DIR)
        
    if os.path.exists(MODEL_PATH):
        print(f"[OK] Model already exists at {MODEL_PATH}")
        return

    print(f"Downloading CNN model from {MODEL_URL}...")
    try:
        urllib.request.urlretrieve(MODEL_URL, MODEL_PATH)
        print(f"[OK] Model downloaded to {MODEL_PATH}")
    except Exception as e:
        print(f"[ERROR] Failed to download model: {e}")
        print("Note: If the URL is broken, the Vision module will automatically use template matching fallback.")

if __name__ == "__main__":
    download_model()
