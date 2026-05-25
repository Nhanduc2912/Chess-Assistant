/**
 * Chess Realtime Assistant — Content Script v6.1
 *
 * FIXES:
 * - Board detection retries every 2s until wc-chess-board found
 * - Flipped detection walks up DOM tree (not just board element)
 * - Ply count uses multiple chess.com selectors + text parsing fallback
 * - sendFen retries on 429 (backend busy)
 * - Arrows only drawn for USER's moves (not opponent's)
 */

const BACKEND_URL = 'http://localhost:5000/api/analyze';
const LATEST_URL  = 'http://localhost:5000/api/analyze/latest';
const RESET_URL   = 'http://localhost:5000/api/analyze/reset';
const DEBOUNCE_MS = 200;
const DRAW_MS     = 2000;

let isEnabled     = true;
let lastFen       = '';
let lastPlyCount  = -1;
let lastDrawnFen  = '';
let lastSentOk    = true;
let arrowSvg      = null;
let debounceTimer = null;
let boardObserver = null;
let _cachedBoard  = null;  // cache the board element once found

// ─── Suppress extension lifecycle errors ───────────────────────
window.addEventListener('unhandledrejection', ev => {
  const m = ev.reason?.message || String(ev.reason);
  if (m.includes('Extension context invalidated') ||
      m.includes('Could not establish connection') ||
      m.includes('Receiving end does not exist')) {
    ev.preventDefault();
  }
});

// ─── Extension context guard ───────────────────────────────────
function ctxOk() { try { return !!chrome.runtime?.id; } catch (_) { return false; } }
function safeSend(msg) { if (ctxOk()) chrome.runtime.sendMessage(msg).catch(() => {}); }
function safeInterval(fn, ms) {
  const id = setInterval(() => { if (!ctxOk()) { clearInterval(id); return; } fn(); }, ms);
  return id;
}

// ─── Piece map ─────────────────────────────────────────────────
const PIECE_MAP = {
  'wp':'P','wn':'N','wb':'B','wr':'R','wq':'Q','wk':'K',
  'bp':'p','bn':'n','bb':'b','br':'r','bq':'q','bk':'k',
};

// ─────────────────────────────────────────────────────────────
// FIND BOARD ELEMENT
// chess.com uses <wc-chess-board> (Web Component).
// It may not exist at page load — retried until found.
// ─────────────────────────────────────────────────────────────
function getBoard() {
  // Return cached if still in DOM
  if (_cachedBoard && document.body.contains(_cachedBoard)) return _cachedBoard;

  // Try multiple selectors in priority order
  const board = document.querySelector('wc-chess-board') ||
                document.querySelector('chess-board');
  if (board) {
    _cachedBoard = board;
    return board;
  }

  // Last resort: .board div (but NOT ideal — no flipped class)
  return document.querySelector('.board');
}

// ─────────────────────────────────────────────────────────────
// BOARD FLIP DETECTION
// chess.com adds 'flipped' class when playing Black.
// Check the board element AND all its ancestors.
// ─────────────────────────────────────────────────────────────
function isBoardFlipped() {
  const board = getBoard();
  if (!board) return false;

  // Walk up the DOM tree — 'flipped' may be on a parent
  let el = board;
  while (el && el !== document.body) {
    if (el.classList?.contains('flipped')) return true;
    el = el.parentElement;
  }

  return false;
}

function getUserColor()  { return isBoardFlipped() ? 'b' : 'w'; }

// ─── Ply count & active color ──────────────────────────────────
function getPlyCount() {
  // Method 1: [data-ply] elements (live games)
  const plyEls = document.querySelectorAll('[data-ply]');
  if (plyEls.length > 0) return plyEls.length;

  // Method 2: .node elements in move list (bot games)
  const nodes = document.querySelectorAll('.move-list .node, .play-controller-scrollable .node');
  if (nodes.length > 0) return nodes.length;

  // Method 3: Vertical move list nodes
  const vertNodes = document.querySelectorAll('vertical-move-list .node');
  if (vertNodes.length > 0) return vertNodes.length;

  // Method 4: Parse move text from any move list container
  // Look for any container with move notation text
  const moveContainers = document.querySelectorAll(
    '.move-list, .play-controller-scrollable, [class*="move-list"], [class*="moveList"]'
  );
  for (const container of moveContainers) {
    const text = container.textContent || '';
    // Count move notation patterns: e4, Nf3, O-O, dxe5, Qxd8+, etc.
    const moves = text.match(
      /\b(?:[KQRBNP]?[a-h]?[1-8]?x?[a-h][1-8](?:=[QRBN])?[+#]?|O-O(?:-O)?)\b/g
    );
    if (moves && moves.length > 0) return moves.length;
  }

  // Method 5: Count highlighted squares on the board (0 = start, 2 = a move was made)
  const board = getBoard();
  if (board) {
    const root = board.shadowRoot || board;
    const highlights = root.querySelectorAll('.highlight, [class*="highlight"]');
    // Each move highlights 2 squares (from + to). If highlights exist, at least 1 ply.
    if (highlights.length >= 2) {
      // We can't tell exact ply from highlights alone, but we know at least 1 move happened.
      // Use this as a signal that the game has started.
      // Try to count from move number elements
      const moveNums = document.querySelectorAll('.move-number, [class*="move-number"]');
      if (moveNums.length > 0) {
        const lastNum = parseInt(moveNums[moveNums.length - 1].textContent);
        if (!isNaN(lastNum)) {
          // Check if black has also moved in the last numbered move
          // by looking at the sibling elements
          const lastMoveContainer = moveNums[moveNums.length - 1].closest('.move, [class*="move"]');
          if (lastMoveContainer) {
            const moveTexts = lastMoveContainer.querySelectorAll('.node, [class*="node"]');
            return (lastNum - 1) * 2 + moveTexts.length;
          }
          return lastNum * 2 - 1; // assume white just moved
        }
      }
      return 1; // fallback: at least 1 ply
    }
  }

  return 0;
}

function getActiveColor() { return getPlyCount() % 2 === 0 ? 'w' : 'b'; }
function isUserTurn()     { return getActiveColor() === getUserColor(); }

// ─── New game detection ────────────────────────────────────────
function checkNewGame(ply) {
  if (lastPlyCount > 4 && ply === 0) {
    lastFen = ''; lastDrawnFen = ''; lastPlyCount = 0; lastSentOk = true;
    fetch(RESET_URL, { method: 'POST' }).catch(() => {});
    clearArrows();
    return true;
  }
  return false;
}

// ─────────────────────────────────────────────────────────────
// READ BOARD — parse pieces from DOM
// ─────────────────────────────────────────────────────────────
function readBoard() {
  const boardEl = getBoard();
  if (!boardEl) return null;

  const root   = boardEl.shadowRoot || boardEl;
  const pieces = root.querySelectorAll('[class*="piece "]');
  if (!pieces.length) return null;

  const grid = Array.from({length:8}, () => Array(8).fill('.'));
  let placed = 0;

  pieces.forEach(el => {
    const parts = (el.className || '').split(/\s+/);
    const piece = parts.find(p => PIECE_MAP[p]);
    const sqCls = parts.find(p => p.startsWith('square-'));
    if (!piece || !sqCls) return;

    const sq = sqCls.slice(7);
    let fi, ri;
    if (/^\d{2}$/.test(sq)) {
      fi = parseInt(sq[0]) - 1;
      ri = parseInt(sq[1]) - 1;
    } else if (/^[a-h][1-8]$/.test(sq)) {
      fi = sq.charCodeAt(0) - 97;
      ri = parseInt(sq[1]) - 1;
    } else return;

    if (fi < 0 || fi > 7 || ri < 0 || ri > 7) return;
    grid[7 - ri][fi] = PIECE_MAP[piece];
    placed++;
  });

  return placed >= 2 ? { grid, placed } : null;
}

function toFen(grid, activeColor) {
  return grid.map(row => {
    let s = '', e = 0;
    row.forEach(c => {
      if (c === '.') e++;
      else { if (e) { s += e; e = 0; } s += c; }
    });
    return s + (e || '');
  }).join('/') + ` ${activeColor} - - 0 1`;
}

// ─── Board rect helpers ────────────────────────────────────────
function getBoardRect() { return getBoard()?.getBoundingClientRect() ?? null; }
function getBoardBbox() {
  const r = getBoardRect();
  if (!r) return null;
  return { x: Math.round(r.left+window.screenX), y: Math.round(r.top+window.screenY),
           w: Math.round(r.width), h: Math.round(r.height) };
}

// ─── Send FEN to backend with retry on 429 ─────────────────────
async function sendFen(fen, isWhiteBottom) {
  for (let attempt = 0; attempt < 4; attempt++) {
    try {
      const resp = await fetch(BACKEND_URL, {
        method: 'POST',
        headers: {'Content-Type':'application/json'},
        body: JSON.stringify({ fen, bbox: getBoardBbox(), isWhiteBottom }),
        signal: AbortSignal.timeout(6000) // Increased timeout for Stockfish
      });
      
      if (resp.status === 429) {
        await new Promise(r => setTimeout(r, 1000 * (attempt + 1)));
        continue;
      }
      
      if (!resp.ok) return;
      
      const data = await resp.json();
      if (!data?.bestMoves?.length) return;

      // Draw immediately
      const analysisTurn = (data.fen?.split(' ')[1]) || 'w';
      const userColor    = getUserColor();

      if (analysisTurn === userColor) {
        lastDrawnFen = data.fen;
        const rect    = getBoardRect();
        const flipped = isBoardFlipped();
        if (rect) drawArrows(data.bestMoves, rect, flipped);
        
        const userScore = data.evaluation;
        const s = (Math.abs(userScore) / 100).toFixed(1);
        safeSend({
          type: 'UPDATE_BADGE',
          text: `${userScore >= 0 ? '+' : '-'}${s}`,
          color: userScore >= 0 ? '#22c55e' : '#ef4444',
          analysis: { ...data, evaluation: userScore }
        });
      }

      lastSentOk = true;
      return;
    } catch (e) {
      console.error('[CA] sendFen error:', e);
      await new Promise(r => setTimeout(r, 500));
    }
  }
  lastSentOk = false;
}

// ─────────────────────────────────────────────────────────────
// SVG ARROW OVERLAY
// ─────────────────────────────────────────────────────────────
function ensureOverlay() {
  if (arrowSvg && document.body.contains(arrowSvg)) return arrowSvg;
  document.getElementById('ca-svg')?.remove();

  const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  svg.id = 'ca-svg';
  svg.style.cssText = 'position:fixed;top:0;left:0;width:100vw;height:100vh;pointer-events:none;z-index:2147483640;overflow:visible;';

  const defs = document.createElementNS('http://www.w3.org/2000/svg', 'defs');
  [['ca0','#22c55e'],['ca1','#84cc16'],['ca2','#94a3b8']].forEach(([id,col]) => {
    const mk = document.createElementNS('http://www.w3.org/2000/svg','marker');
    mk.setAttribute('id',id); mk.setAttribute('markerWidth','8');
    mk.setAttribute('markerHeight','6'); mk.setAttribute('refX','7');
    mk.setAttribute('refY','3'); mk.setAttribute('orient','auto');
    mk.setAttribute('markerUnits','strokeWidth');
    const p = document.createElementNS('http://www.w3.org/2000/svg','polygon');
    p.setAttribute('points','0 0,8 3,0 6'); p.setAttribute('fill',col);
    mk.appendChild(p); defs.appendChild(mk);
  });
  svg.appendChild(defs);
  document.body.appendChild(svg);
  arrowSvg = svg;
  return svg;
}

function clearArrows() {
  if (!arrowSvg) return;
  [...arrowSvg.querySelectorAll('line,circle')].forEach(e => e.remove());
}

function sqXY(file, rank, rect, flipped) {
  const fi = file.charCodeAt(0) - 97;
  const ri = parseInt(rank) - 1;
  const w  = rect.width / 8, h = rect.height / 8;
  const col = flipped ? 7 - fi : fi;
  const row = flipped ? ri : 7 - ri;
  return { x: rect.left + (col+0.5)*w, y: rect.top + (row+0.5)*h };
}

function drawArrows(moves, rect, flipped) {
  const svg = ensureOverlay();
  clearArrows();
  const colors = ['#22c55e','#84cc16','#94a3b8'];
  const widths = [10, 7, 5];
  const alphas = [0.90, 0.65, 0.38];

  moves.slice(0,3).forEach((mi, i) => {
    const mv = mi.move || '';
    if (mv.length < 4) return;
    const from = sqXY(mv[0], mv[1], rect, flipped);
    const to   = sqXY(mv[2], mv[3], rect, flipped);

    const c = document.createElementNS('http://www.w3.org/2000/svg','circle');
    c.setAttribute('cx', from.x); c.setAttribute('cy', from.y);
    c.setAttribute('r', rect.width/16*0.48);
    c.setAttribute('fill','none'); c.setAttribute('stroke', colors[i]);
    c.setAttribute('stroke-width','3'); c.setAttribute('stroke-opacity', alphas[i]*0.7);
    svg.appendChild(c);

    const ln = document.createElementNS('http://www.w3.org/2000/svg','line');
    ln.setAttribute('x1',from.x); ln.setAttribute('y1',from.y);
    ln.setAttribute('x2',to.x);   ln.setAttribute('y2',to.y);
    ln.setAttribute('stroke', colors[i]);
    ln.setAttribute('stroke-width', widths[i]);
    ln.setAttribute('stroke-opacity', alphas[i]);
    ln.setAttribute('stroke-linecap','round');
    ln.setAttribute('marker-end',`url(#ca${i})`);
    svg.appendChild(ln);
  });
}

// ─────────────────────────────────────────────────────────────
// POLL /latest → draw arrows
// Only show moves that are FOR the user (not opponent's moves)
// ─────────────────────────────────────────────────────────────
async function pollAndDraw() {
  if (!isEnabled || !ctxOk()) return;

  try {
    const resp = await fetch(LATEST_URL, { signal: AbortSignal.timeout(1000) });
    if (resp.status === 204 || !resp.ok) return;
    const data = await resp.json();
    if (!data?.bestMoves?.length) return;

    // Only draw arrows when the analysis is for the USER's color
    const analysisTurn = (data.fen?.split(' ')[1]) || 'w';
    const userColor    = getUserColor();

    if (analysisTurn !== userColor) {
      clearArrows();
      return;
    }

    if (data.fen === lastDrawnFen) return;
    lastDrawnFen = data.fen;

    const rect    = getBoardRect();
    const flipped = isBoardFlipped();
    if (rect) drawArrows(data.bestMoves, rect, flipped);

    const userScore = data.evaluation;
    const s = (Math.abs(userScore) / 100).toFixed(1);
    safeSend({
      type: 'UPDATE_BADGE',
      text: `${userScore >= 0 ? '+' : '-'}${s}`,
      color: userScore >= 0 ? '#22c55e' : '#ef4444',
      analysis: { ...data, evaluation: userScore }
    });
  } catch (_) {}
}

// ─────────────────────────────────────────────────────────────
// CORE: board changed → read & send FEN
// ─────────────────────────────────────────────────────────────
function onBoardChanged() {
  if (!isEnabled || !ctxOk()) return;
  clearTimeout(debounceTimer);
  debounceTimer = setTimeout(() => {
    const result = readBoard();
    if (!result) return;

    const activeColor   = getActiveColor();
    const isWhiteBottom = !isBoardFlipped();
    const fen           = toFen(result.grid, activeColor);

    if (fen === lastFen && lastSentOk) return;
    lastFen = fen;
    lastSentOk = false;

    console.log(`[CA v6.1] FEN | active=${activeColor} | user=${getUserColor()} | flipped=${isBoardFlipped()} | ply=${getPlyCount()}`);
    sendFen(fen, isWhiteBottom);
    safeSend({ type: 'FEN_UPDATE', fen });
  }, DEBOUNCE_MS);
}

// ─────────────────────────────────────────────────────────────
// MutationObserver: watch board DOM for changes
// ─────────────────────────────────────────────────────────────
function attachObserver() {
  boardObserver?.disconnect();
  const boardEl = getBoard();
  if (!boardEl) return false;

  const root = boardEl.shadowRoot || boardEl;
  boardObserver = new MutationObserver(() => {
    if (!ctxOk()) { boardObserver?.disconnect(); boardObserver = null; return; }
    const ply = getPlyCount();
    checkNewGame(ply);
    lastPlyCount = ply;
    onBoardChanged();
  });
  boardObserver.observe(root, {
    childList: true, subtree: true,
    attributes: true, attributeFilter: ['class']
  });

  // Also watch the board and parents for 'flipped' class changes
  let el = boardEl;
  while (el && el !== document.body) {
    const observer = new MutationObserver(() => onBoardChanged());
    observer.observe(el, { attributes: true, attributeFilter: ['class'] });
    el = el.parentElement;
  }

  return true;
}

// ─────────────────────────────────────────────────────────────
// Message handler
// ─────────────────────────────────────────────────────────────
chrome.runtime.onMessage.addListener((msg, _sender, reply) => {
  try {
    if (msg.type === 'TOGGLE') {
      isEnabled = msg.enabled;
      if (!isEnabled) { lastFen = ''; clearArrows(); }
      reply({ ok: true, isEnabled });
    } else if (msg.type === 'GET_STATUS') {
      const r = readBoard();
      reply({
        isEnabled, lastFen,
        boardDetected : !!r,
        pieceCount    : r?.placed ?? 0,
        flipped       : isBoardFlipped(),
        userColor     : getUserColor(),
        activeColor   : getActiveColor(),
        isUserTurn    : isUserTurn(),
        plyCount      : getPlyCount()
      });
    }
  } catch (_) {}
  return true;
});

// ─────────────────────────────────────────────────────────────
// START — with retry if board not found yet
// ─────────────────────────────────────────────────────────────
let startAttempts = 0;

function start() {
  if (!ctxOk()) return;
  startAttempts++;

  const boardEl = getBoard();
  if (!boardEl || boardEl.tagName === 'DIV') {
    // Board not found yet (or wrong element) — retry
    if (startAttempts < 30) {
      console.log(`[CA v6.1] Board not ready (attempt ${startAttempts}), retrying in 2s...`);
      setTimeout(start, 2000);
      return;
    }
    // After 60s of retrying, use whatever we have
    console.log('[CA v6.1] Giving up waiting for wc-chess-board, using fallback');
  }

  const attached = attachObserver();
  safeInterval(pollAndDraw, DRAW_MS);

  // Fallback poll every 2s
  safeInterval(() => {
    // Re-check for board if we didn't find wc-chess-board
    if (!_cachedBoard || _cachedBoard.tagName === 'DIV') {
      const newBoard = document.querySelector('wc-chess-board') || document.querySelector('chess-board');
      if (newBoard && newBoard !== _cachedBoard) {
        _cachedBoard = newBoard;
        console.log(`[CA v6.1] Found board: ${newBoard.tagName}, re-attaching observer`);
        attachObserver();
      }
    }

    if (!boardObserver) attachObserver();
    const result = readBoard();
    if (!result) return;
    const fen = toFen(result.grid, getActiveColor());
    if (fen !== lastFen || !lastSentOk) onBoardChanged();
  }, 2000);

  const flipped = isBoardFlipped();
  console.log(
    `[CA v6.1] Started | site=${location.hostname}` +
    ` | board=${boardEl?.tagName ?? 'NOT FOUND'}` +
    ` | flipped=${flipped} | userColor=${getUserColor()}` +
    ` | ply=${getPlyCount()}`
  );
}

setTimeout(start, 2000);
