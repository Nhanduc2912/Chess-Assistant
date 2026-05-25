import cv2
import sys
from detector import detect_board, BoardNotFoundError
from fen_converter import FenConverter

img = cv2.imread("../../imageError/error1.png")
if img is None:
    print("Could not read image")
    sys.exit(1)

try:
    warped, bbox = detect_board(img)
    print(f"Success! Bbox: {bbox}")
    
    cv2.imwrite("warped_test.png", warped)
    
    converter = FenConverter(threshold=0.85)
    fen, is_white_bottom = converter.convert_to_fen(warped)
    print(f"FEN: {fen}, is_white_bottom: {is_white_bottom}")
    
except BoardNotFoundError:
    print("Failed to find board contour.")
except Exception as e:
    print(f"Exception during conversion: {e}")
