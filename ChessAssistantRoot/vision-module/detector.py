import cv2
import numpy as np
import mss
from typing import Tuple, Dict

class BoardNotFoundError(Exception):
    pass

def capture_screen():
    with mss.mss() as sct:
        # Use primary monitor (monitor 1) to avoid secondary screen false positives
        monitor = sct.monitors[1]
        img = np.array(sct.grab(monitor))
        return img, monitor

def detect_board(image: np.ndarray, min_area: int = 40000) -> Tuple[np.ndarray, Dict[str, int]]:
    """
    Detects the chess board in the image, warps it to 512x512, and returns
    the warped image and the original bounding box.
    """
    gray = cv2.cvtColor(image, cv2.COLOR_BGRA2GRAY)
    
    # 1. Edge detection
    edges = cv2.Canny(gray, 50, 150, apertureSize=3)
    
    # 2. Find contours
    contours, _ = cv2.findContours(edges, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    
    board_contour = None
    max_area = 0
    
    for cnt in contours:
        area = cv2.contourArea(cnt)
        if area > min_area:
            # Approximate to a polygon
            peri = cv2.arcLength(cnt, True)
            approx = cv2.approxPolyDP(cnt, 0.02 * peri, True)
            
            # The board should be a rectangle (4 points)
            if len(approx) == 4 and area > max_area:
                board_contour = approx
                max_area = area
                
    if board_contour is None:
        raise BoardNotFoundError("Could not find a suitable board contour.")
        
    # 3. Get bounding box
    x, y, w, h = cv2.boundingRect(board_contour)
    bbox = {"x": int(x), "y": int(y), "w": int(w), "h": int(h)}
    
    # 4. Perspective warp to 512x512
    # Ensure points are ordered: top-left, top-right, bottom-right, bottom-left
    pts = board_contour.reshape(4, 2)
    rect = np.zeros((4, 2), dtype="float32")
    
    s = pts.sum(axis=1)
    rect[0] = pts[np.argmin(s)]
    rect[2] = pts[np.argmax(s)]
    
    diff = np.diff(pts, axis=1)
    rect[1] = pts[np.argmin(diff)]
    rect[3] = pts[np.argmax(diff)]
    
    dst = np.array([
        [0, 0],
        [511, 0],
        [511, 511],
        [0, 511]
    ], dtype="float32")
    
    M = cv2.getPerspectiveTransform(rect, dst)
    warped = cv2.warpPerspective(image, M, (512, 512))
    
    # Remove alpha channel if it exists
    if warped.shape[2] == 4:
        warped = cv2.cvtColor(warped, cv2.COLOR_BGRA2BGR)
        
    return warped, bbox
