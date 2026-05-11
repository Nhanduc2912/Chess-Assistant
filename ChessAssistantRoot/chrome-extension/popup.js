/**
 * Popup JS — Handles UI updates and communicates with content script.
 */

const BACKEND_STATUS_URL = 'http://localhost:5000/api/analyze/status';

// Suppress known Chrome extension lifecycle errors in popup context
window.addEventListener('unhandledrejection', ev => {
  const msg = ev.reason?.message || String(ev.reason);
  if (msg.includes('Extension context invalidated') ||
      msg.includes('Could not establish connection') ||
      msg.includes('Receiving end does not exist')) {
    ev.preventDefault();
  }
});

const CLASS_COLORS = {
  'Brilliant': '#a855f7', 'Best': '#22c55e', 'Good': '#84cc16',
  'Inaccuracy': '#eab308', 'Mistake': '#f97316', 'Blunder': '#ef4444',
};
const CLASS_EMOJI = {
  'Brilliant': '✨', 'Best': '✅', 'Good': '👍',
  'Inaccuracy': '⚠️', 'Mistake': '❌', 'Blunder': '💀',
};

let latestAnalysis = null;
let engineOnline = false;

// ─────────────────────────────────────────────────────────────
// DOM elements
// ─────────────────────────────────────────────────────────────
const engineDot    = document.getElementById('engineDot');
const engineStatus = document.getElementById('engineStatus');
const mainContent  = document.getElementById('mainContent');
const toggleSwitch = document.getElementById('toggleSwitch');
const toggleLabel  = document.getElementById('toggleLabel');
const waitingMsg   = document.getElementById('waitingMsg');

// ─────────────────────────────────────────────────────────────
// Check backend health
// ─────────────────────────────────────────────────────────────
async function checkEngineStatus() {
  try {
    const resp = await fetch(BACKEND_STATUS_URL, { signal: AbortSignal.timeout(1500) });
    if (resp.ok) {
      const data = await resp.json();
      engineOnline = data.stockfishAlive;
      engineDot.className = 'dot ' + (engineOnline ? 'green' : 'yellow');
      engineStatus.textContent = engineOnline
        ? `Stockfish Online • ${data.cacheEntries} cached`
        : 'Backend reachable — Stockfish starting...';
      return;
    }
  } catch (e) {/* ignore */}
  engineOnline = false;
  engineDot.className = 'dot red';
  engineStatus.textContent = 'Backend offline — run start_assistant.bat';
}

// ─────────────────────────────────────────────────────────────
// Render analysis results
// ─────────────────────────────────────────────────────────────
function renderAnalysis(analysis) {
  if (!analysis) return;
  latestAnalysis = analysis;

  const color = CLASS_COLORS[analysis.classification] || '#94a3b8';
  const emoji = CLASS_EMOJI[analysis.classification] || '';
  const scorePawns = (analysis.evaluation / 100).toFixed(2);
  const sign = analysis.evaluation >= 0 ? '+' : '';

  const movesHtml = (analysis.bestMoves || []).slice(0, 3).map((m, i) => `
    <div class="move-row ${i === 0 ? 'best' : 'alt'}">
      <span class="move-name" style="color: ${i === 0 ? '#86efac' : '#cbd5e1'}">${m.move}</span>
      <span class="move-score">${m.score >= 0 ? '+' : ''}${(m.score / 100).toFixed(2)}</span>
    </div>
  `).join('');

  mainContent.innerHTML = `
    <div class="content">
      <div class="eval-section">
        <div class="section-label">Evaluation</div>
        <div class="eval-row">
          <span class="eval-score">${sign}${scorePawns}</span>
          ${analysis.classification ? `
          <span class="class-badge" style="background:${color}22; color:${color}; border:1px solid ${color}44;">
            ${emoji} ${analysis.classification}
          </span>` : ''}
        </div>
      </div>

      <div class="moves-section">
        <div class="section-label">Top Moves</div>
        ${movesHtml || '<div style="color:#475569;font-size:12px">No moves found</div>'}
      </div>

      <div class="section-label" style="margin-bottom:6px">Current Position</div>
      <div class="fen-box">${analysis.fen || '—'}</div>
    </div>
  `;
}

// ─────────────────────────────────────────────────────────────
// Toggle enable/disable
// ─────────────────────────────────────────────────────────────
toggleSwitch.addEventListener('change', () => {
  const enabled = toggleSwitch.checked;
  toggleLabel.textContent = enabled ? 'On' : 'Off';

  chrome.tabs.query({ active: true, currentWindow: true }, (tabs) => {
    if (!tabs[0]) return;
    chrome.tabs.sendMessage(tabs[0].id, { type: 'TOGGLE', enabled })
      .catch(() => {}); // content script might not be ready yet
  });

  if (!enabled) {
    mainContent.innerHTML = `<div class="waiting"><div>Paused. Toggle On to resume.</div></div>`;
  }
});

// ─────────────────────────────────────────────────────────────
// Listen for real-time analysis from background/content
// ─────────────────────────────────────────────────────────────
chrome.runtime.onMessage.addListener((msg) => {
  if (msg.type === 'ANALYSIS_RESULT' || msg.type === 'FEN_UPDATE') {
    if (msg.analysis) {
      renderAnalysis(msg.analysis);
    } else if (msg.fen) {
      // FEN sent but analysis not received yet — show waiting
      if (waitingMsg) waitingMsg.textContent = 'Position detected. Analyzing...';
    }
  }
});

// ─────────────────────────────────────────────────────────────
// Check board status from content script
// ─────────────────────────────────────────────────────────────
function checkBoardStatus() {
  chrome.tabs.query({ active: true, currentWindow: true }, (tabs) => {
    if (!tabs[0]) return;
    if (chrome.runtime.lastError) return; // tab query failed

    const url = tabs[0].url || '';
    const isChessSite = url.includes('chess.com') || url.includes('lichess.org');

    if (!isChessSite) {
      mainContent.innerHTML = `
        <div class="no-board">
          <strong>⚠️ Not on a chess site</strong>
          Please open Chess.com or Lichess.
        </div>`;
      return;
    }

    // sendMessage can throw "Receiving end does not exist" if content
    // script isn't loaded yet. Use Promise form + catch.
    chrome.tabs.sendMessage(tabs[0].id, { type: 'GET_STATUS' })
      .then(response => {
        if (!response) return;
        if (response.boardDetected) {
          const colorIcon  = response.userColor === 'w' ? '♞' : '♚';
          const colorLabel = response.userColor === 'w' ? 'White' : 'Black';
          const turnLabel  = response.isUserTurn ? '🟢 Your turn' : '⏳ Opponent thinking...';
          engineStatus.textContent = `Stockfish Online • Playing ${colorLabel} ${colorIcon} • ${turnLabel}`;

          if (!response.isUserTurn) {
            mainContent.innerHTML = `
              <div class="waiting">
                <div style="font-size:24px;margin-bottom:8px">⏳</div>
                <div style="color:#94a3b8">Waiting for opponent's move...</div>
              </div>`;
          }
        } else {
          mainContent.innerHTML = `
            <div class="no-board">
              <strong>⚠️ Board not detected</strong>
              Open a game page on Chess.com.
            </div>`;
        }
      })
      .catch(() => {
        // Content script not yet loaded — show helpful message, not error
        mainContent.innerHTML = `
          <div class="no-board">
            <strong>⚠️ Not ready</strong>
            Please refresh (F5) the Chess.com page.
          </div>`;
      });
  });
}

// ─────────────────────────────────────────────────────────────
// Poll /api/analyze/latest every 800ms for fresh analysis
// (More reliable than WebSocket for an extension popup)
// ─────────────────────────────────────────────────────────────
let lastFenSeen = '';

async function pollForResult() {
  if (!engineOnline) return;
  try {
    const resp = await fetch('http://localhost:5000/api/analyze/latest', {
      signal: AbortSignal.timeout(1000)
    });
    if (resp.status === 204) return; // no result yet
    if (!resp.ok) return;
    const analysis = await resp.json();
    if (analysis && analysis.fen !== lastFenSeen) {
      lastFenSeen = analysis.fen;
      renderAnalysis(analysis);
    }
  } catch (e) { /* backend offline */ }
}

// ─────────────────────────────────────────────────────────────
// Init
// ─────────────────────────────────────────────────────────────
(async function init() {
  await checkEngineStatus();
  checkBoardStatus();

  // Refresh engine status every 3s
  setInterval(checkEngineStatus, 3000);

  // Poll for latest analysis every 800ms
  setInterval(pollForResult, 800);
})();

