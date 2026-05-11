import time
import yaml
import requests
import traceback
import os
from utils.hash_utils import compute_fen_hash
from utils.bbox_utils import is_valid_bbox
from detector import capture_screen, detect_board, BoardNotFoundError
from fen_converter import FenConverter
from download_templates import download_templates

def load_config(path="config.yaml"):
    try:
        with open(path, "r") as f:
            return yaml.safe_load(f)
    except Exception as e:
        print(f"[WARN] Failed to load config.yaml: {e}. Using defaults.")
        return {
            "vision": {"fps_target": 10, "cnn_confidence_threshold": 0.85, "board_detection_min_area": 40000},
            "backend": {"url": "http://localhost:5000/api/analyze", "timeout_seconds": 2}
        }

def log_event(level: str, message: str):
    ts = time.strftime("%Y-%m-%d %H:%M:%S")
    log_msg = f"[{ts}] [{level}] [VISION] {message}"
    print(log_msg)
    try:
        with open("../.agent_config/system.log", "a") as f:
            f.write(log_msg + "\n")
    except:
        pass

def main():
    config = load_config()
    fps_target = config.get("vision", {}).get("fps_target", 10)
    delay_s = 1.0 / fps_target if fps_target > 0 else 0.1
    backend_url = config.get("backend", {}).get("url", "http://localhost:5000/api/analyze")
    timeout = config.get("backend", {}).get("timeout_seconds", 2)
    threshold = config.get("vision", {}).get("cnn_confidence_threshold", 0.85)
    min_area = config.get("vision", {}).get("board_detection_min_area", 40000)

    log_event("INFO", f"Starting Vision Module (Target FPS: {fps_target})")
    
    # Ensure templates exist before starting
    download_templates()
    
    converter = FenConverter(threshold=threshold)
    last_fen_hash = ""

    while True:
        start_time = time.time()
        try:
            # 1. Capture screen
            img, _ = capture_screen()
            
            # 2. Detect and warp board
            warped_board, bbox = detect_board(img, min_area=min_area)
            
            if not is_valid_bbox(bbox):
                log_event("WARN", "Invalid bbox received")
                continue

            # 3. Convert to FEN
            fen, is_white_bottom = converter.convert_to_fen(warped_board)
            new_hash = compute_fen_hash(fen)
            
            # 4. Hash Diff
            if new_hash != last_fen_hash:
                log_event("INFO", f"FEN changed: {fen}")
                
                # 5. POST to backend
                payload = {"fen": fen, "bbox": bbox, "isWhiteBottom": is_white_bottom}
                try:
                    response = requests.post(backend_url, json=payload, timeout=timeout)
                    if response.status_code == 200:
                        last_fen_hash = new_hash
                    elif response.status_code == 503:
                        log_event("ERROR", f"Backend Stockfish timeout on FEN: {fen}")
                    else:
                        log_event("WARN", f"Backend returned status {response.status_code}")
                except requests.RequestException as e:
                    log_event("ERROR", f"Failed to send POST to backend: {e}")
                    
        except BoardNotFoundError:
            log_event("WARN", "Board not detected in frame")
        except ValueError as ve:
            log_event("WARN", str(ve))
        except Exception as e:
            log_event("CRITICAL", f"Unexpected error: {traceback.format_exc()}")
            
        # Rate limit
        elapsed = time.time() - start_time
        sleep_time = max(0, delay_s - elapsed)
        time.sleep(sleep_time)

if __name__ == "__main__":
    main()
