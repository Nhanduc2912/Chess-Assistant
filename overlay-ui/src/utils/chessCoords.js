// Algebraic columns (files) and rows (ranks)
const cols = ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h'];
const rows = ['1', '2', '3', '4', '5', '6', '7', '8'];

/**
 * Converts algebraic move (e.g. "e2e4") to a pair of 0-7 indices.
 * returns: { start: { col, row }, end: { col, row } }
 * Note: row 0 is rank 1, row 7 is rank 8.
 *       col 0 is file a, col 7 is file h.
 */
export const parseAlgebraicMove = (move) => {
  if (!move || move.length < 4) return null;
  
  const startFile = move[0];
  const startRank = move[1];
  const endFile = move[2];
  const endRank = move[3];
  
  return {
    start: {
      col: cols.indexOf(startFile),
      row: rows.indexOf(startRank)
    },
    end: {
      col: cols.indexOf(endFile),
      row: rows.indexOf(endRank)
    }
  };
};
