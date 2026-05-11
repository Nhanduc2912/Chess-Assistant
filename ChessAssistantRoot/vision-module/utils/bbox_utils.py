from typing import Dict

def create_bbox(x: int, y: int, w: int, h: int) -> Dict[str, int]:
    """
    Creates a dictionary representing a bounding box.
    """
    return {
        "x": int(x),
        "y": int(y),
        "w": int(w),
        "h": int(h)
    }

def is_valid_bbox(bbox: Dict[str, int]) -> bool:
    """
    Validates if a bbox dictionary is well-formed and has positive dimensions.
    """
    if not bbox:
        return False
        
    required_keys = {"x", "y", "w", "h"}
    if not required_keys.issubset(bbox.keys()):
        return False
        
    if bbox["w"] <= 0 or bbox["h"] <= 0:
        return False
        
    return True
