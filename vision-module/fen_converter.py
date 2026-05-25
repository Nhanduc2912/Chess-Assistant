import cv2
import numpy as np
import chess
import os

try:
    import onnxruntime as ort
    ONNX_AVAILABLE = True
except ImportError:
    ONNX_AVAILABLE = False

PIECE_CLASSES = ['B', 'K', 'N', 'P', 'Q', 'R', 'b', 'k', 'n', 'p', 'q', 'r', '.']

# Template filename convention: <FEN_CHAR>_<white|black>.png
TEMPLATE_DIR = "templates/chesscom_default"

# Names that map FEN character → template filename
TEMPLATE_MAP = {
    'P': 'P_white.png', 'N': 'N_white.png', 'B': 'B_white.png',
    'R': 'R_white.png', 'Q': 'Q_white.png', 'K': 'K_white.png',
    'p': 'p_black.png', 'n': 'n_black.png', 'b': 'b_black.png',
    'r': 'r_black.png', 'q': 'q_black.png', 'k': 'k_black.png',
}

# Square background detection thresholds
# Chess.com default board: light #EEEED2, dark #769656
LIGHT_THRESH = 180
DARK_THRESH  = 100

class FenConverter:
    def __init__(self, model_path: str = "models/chess_piece_cnn.onnx", threshold: float = 0.85):
        self.threshold = threshold
        self.ort_session = None
        self.use_cnn = False
        
        if ONNX_AVAILABLE and os.path.exists(model_path):
            try:
                self.ort_session = ort.InferenceSession(model_path)
                self.use_cnn = True
                print(f"[INFO] Loaded CNN model from {model_path}")
            except Exception as e:
                print(f"[WARN] Failed to load ONNX model: {e}")
        else:
            print("[WARN] CNN model not found. Using template matching fallback.")

        # Load templates
        self.templates = self._load_templates()
        if self.templates:
            print(f"[INFO] Loaded {len(self.templates)} piece templates from {TEMPLATE_DIR}")
        else:
            print(f"[WARN] No templates found in {TEMPLATE_DIR}. Run download_templates.py first.")

    def _load_templates(self) -> dict:
        """Load all piece template images with their binary alpha masks."""
        templates = {}
        if not os.path.exists(TEMPLATE_DIR):
            return templates
        for fen_char, fname in TEMPLATE_MAP.items():
            fpath = os.path.join(TEMPLATE_DIR, fname)
            if os.path.exists(fpath):
                img = cv2.imread(fpath, cv2.IMREAD_UNCHANGED)
                if img is not None:
                    img = cv2.resize(img, (64, 64))
                    if len(img.shape) == 3 and img.shape[2] == 4:
                        gray = cv2.cvtColor(img, cv2.COLOR_BGRA2GRAY)
                        # Extract and binarize alpha mask
                        _, mask_bin = cv2.threshold(img[:, :, 3], 127, 255, cv2.THRESH_BINARY)
                        templates[fen_char] = (gray, mask_bin)
        return templates

    def _predict_square_cnn(self, square_img: np.ndarray) -> tuple:
        if not self.use_cnn or self.ort_session is None:
            return '.', 0.0
        gray = cv2.cvtColor(square_img, cv2.COLOR_BGR2GRAY)
        resized = cv2.resize(gray, (64, 64))
        img_normalized = resized.astype(np.float32) / 255.0
        input_data = np.expand_dims(np.expand_dims(img_normalized, axis=0), axis=0)
        input_name = self.ort_session.get_inputs()[0].name
        outputs = self.ort_session.run(None, {input_name: input_data})
        logits = outputs[0][0]
        exp_logits = np.exp(logits - np.max(logits))
        probs = exp_logits / exp_logits.sum()
        best_idx = np.argmax(probs)
        return PIECE_CLASSES[best_idx], float(probs[best_idx])

    def _is_square_empty(self, square_gray: np.ndarray) -> bool:
        """
        Heuristic: if the center region of the square is very uniform in colour
        (std dev < 15), it's likely an empty square. Pieces always create significant
        contrast against the background.
        """
        h, w = square_gray.shape
        center = square_gray[h//4:3*h//4, w//4:3*w//4]
        return float(center.std()) < 15.0

    def _predict_square_template(self, square_img: np.ndarray) -> str:
        """
        MSE based template matching to perfectly identify piece type and color.
        """
        if not self.templates:
            return '.'

        gray = cv2.cvtColor(square_img, cv2.COLOR_BGR2GRAY)
        sq64 = cv2.resize(gray, (64, 64))

        if self._is_square_empty(sq64):
            return '.'

        best_piece = '.'
        best_mse = float('inf')

        for fen_char, (tmpl, mask) in self.templates.items():
            result = cv2.matchTemplate(sq64, tmpl, cv2.TM_SQDIFF, mask=mask)
            sqdiff = float(np.min(result))
            
            mask_area = np.count_nonzero(mask)
            if mask_area == 0: continue
            
            mse = sqdiff / mask_area
            
            if mse < best_mse:
                best_mse = mse
                best_piece = fen_char

        if best_mse > 15000: # Threshold for MSE 
            return '.'

        return best_piece

    def convert_to_fen(self, warped_board: np.ndarray) -> tuple[str, bool]:
        """
        Converts the 512x512 warped board image to a FEN string.
        Auto-detects if white is at the bottom based on piece positions.
        Returns: (fen_string, is_white_bottom)
        """
        sq = warped_board.shape[0] // 8
        board_array = []

        for row in range(8):
            current_row = []
            for col in range(8):
                y1, y2 = row * sq, (row + 1) * sq
                x1, x2 = col * sq, (col + 1) * sq
                square_img = warped_board[y1:y2, x1:x2]

                if self.use_cnn:
                    piece, prob = self._predict_square_cnn(square_img)
                    if prob < self.threshold:
                        piece = self._predict_square_template(square_img)
                else:
                    piece = self._predict_square_template(square_img)

                current_row.append(piece)
            board_array.append(current_row)

        # Auto-detect orientation: count pieces in the bottom 2 rows (index 6 and 7 of the image)
        white_bottom_score = sum(1 for r in board_array[6:8] for p in r if p.isupper())
        black_bottom_score = sum(1 for r in board_array[6:8] for p in r if p.islower())
        
        is_white_bottom = white_bottom_score >= black_bottom_score

        if not is_white_bottom:
            board_array = [r[::-1] for r in board_array[::-1]]

        # Build FEN rank strings
        fen_parts = []
        for row in board_array:
            empty = 0
            rank_str = ""
            for piece in row:
                if piece == '.':
                    empty += 1
                else:
                    if empty:
                        rank_str += str(empty)
                        empty = 0
                    rank_str += piece
            if empty:
                rank_str += str(empty)
            fen_parts.append(rank_str)

        fen = "/".join(fen_parts) + " w KQkq - 0 1"

        if fen.startswith("8/8/8/8/8/8/8/8"):
            raise ValueError(f"Detected empty board: {fen}")

        try:
            chess.Board(fen)
            return fen, is_white_bottom
        except ValueError as e:
            raise ValueError(f"Generated invalid FEN: {fen} ({e})")
