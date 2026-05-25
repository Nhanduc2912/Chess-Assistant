import hashlib

def compute_fen_hash(fen: str) -> str:
    """
    Computes a SHA-256 hash of the given FEN string.
    Only the board part of the FEN is hashed to ignore move counts and turn 
    if we just want to know if the board state changed visually.
    (Optional: depending on requirements, we might hash the full FEN).
    Here we hash the full FEN for simplicity, but strip whitespace.
    """
    if not fen:
        return ""
    
    fen_normalized = fen.strip()
    return hashlib.sha256(fen_normalized.encode('utf-8')).hexdigest()

def is_fen_changed(current_fen: str, last_fen: str) -> bool:
    """
    Compares two FENs by their hashes.
    """
    return compute_fen_hash(current_fen) != compute_fen_hash(last_fen)
