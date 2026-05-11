import { useMemo } from 'react';
import { parseAlgebraicMove } from '../utils/chessCoords';

/**
 * Maps an algebraic move to pixel coordinates based on the bounding box.
 * 
 * @param {string} move - The algebraic move (e.g. "e2e4")
 * @param {Object} bbox - The bounding box of the board on screen {x, y, w, h}
 * @param {boolean} isWhiteBottom - True if white is at the bottom of the screen (default view)
 * @returns {Object} {x1, y1, x2, y2} - The start and end coordinates in pixels
 */
export const useCoordinateMap = (move, bbox, isWhiteBottom = true) => {
  return useMemo(() => {
    if (!move || !bbox) return null;

    const parsed = parseAlgebraicMove(move);
    if (!parsed) return null;

    const { start, end } = parsed;
    
    // Width and height of a single square
    const squareW = bbox.w / 8;
    const squareH = bbox.h / 8;
    
    // Calculate 0-based visual column and row
    // If white is at the bottom, rank 1 (row index 0) is visually at the bottom (visual row 7)
    // If black is at the bottom, rank 1 (row index 0) is visually at the top (visual row 0)
    
    const getVisualCoord = (algebraicCol, algebraicRow) => {
      let visualCol = isWhiteBottom ? algebraicCol : 7 - algebraicCol;
      let visualRow = isWhiteBottom ? 7 - algebraicRow : algebraicRow;
      
      return {
        x: bbox.x + visualCol * squareW + squareW / 2,
        y: bbox.y + visualRow * squareH + squareH / 2
      };
    };

    const startCoord = getVisualCoord(start.col, start.row);
    const endCoord = getVisualCoord(end.col, end.row);

    return {
      x1: startCoord.x,
      y1: startCoord.y,
      x2: endCoord.x,
      y2: endCoord.y
    };
  }, [move, bbox, isWhiteBottom]);
};
