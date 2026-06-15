/**
 * Background Service Worker v3
 * Handles badge updates, message routing, and proxies requests to localhost backend
 * to bypass browser Mixed Content (HTTPS -> HTTP) and CORS/CSP restrictions on chess.com.
 */

const BACKEND_URL = 'http://localhost:5000/api/analyze';
const LATEST_URL  = 'http://localhost:5000/api/analyze/latest';
const RESET_URL   = 'http://localhost:5000/api/analyze/reset';

chrome.runtime.onInstalled.addListener(() => {
  // Set initial badge
  chrome.action.setBadgeText({ text: '♟' });
  chrome.action.setBadgeBackgroundColor({ color: '#1e3a5f' });
});

chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
  if (msg.type === 'UPDATE_BADGE') {
    // Show eval score in badge (e.g., "+1.5" or "-0.3")
    const text = msg.text || '';
    chrome.action.setBadgeText({ text: text.length > 5 ? text.slice(0, 5) : text });
    chrome.action.setBadgeBackgroundColor({ color: msg.color || '#22c55e' });

    // Forward to popup if it's open
    chrome.runtime.sendMessage({ type: 'ANALYSIS_RESULT', analysis: msg.analysis }).catch(() => {});
  }

  if (msg.type === 'FEN_UPDATE') {
    chrome.runtime.sendMessage(msg).catch(() => {});
  }

  // Proxy ANALYZE_REQUEST to the C# Backend
  if (msg.type === 'ANALYZE_REQUEST') {
    fetch(BACKEND_URL, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ fen: msg.fen, bbox: msg.bbox, isWhiteBottom: msg.isWhiteBottom }),
      signal: AbortSignal.timeout(6000)
    })
    .then(async resp => {
      if (resp.status === 429) {
        sendResponse({ status: 429 });
      } else if (!resp.ok) {
        sendResponse({ error: `HTTP error ${resp.status}` });
      } else {
        const data = await resp.json();
        sendResponse({ ok: true, data });
      }
    })
    .catch(err => {
      sendResponse({ error: err.message || String(err) });
    });
    return true; // Keep message channel open for async response
  }

  // Proxy LATEST_REQUEST to the C# Backend
  if (msg.type === 'LATEST_REQUEST') {
    fetch(LATEST_URL, { signal: AbortSignal.timeout(2000) })
    .then(async resp => {
      if (resp.status === 204) {
        sendResponse({ status: 204 });
      } else if (!resp.ok) {
        sendResponse({ error: `HTTP error ${resp.status}` });
      } else {
        const data = await resp.json();
        sendResponse({ ok: true, data });
      }
    })
    .catch(err => {
      sendResponse({ error: err.message || String(err) });
    });
    return true; // Keep message channel open for async response
  }

  // Proxy RESET_REQUEST to the C# Backend
  if (msg.type === 'RESET_REQUEST') {
    fetch(RESET_URL, { method: 'POST', signal: AbortSignal.timeout(2000) })
    .then(async resp => {
      sendResponse({ ok: resp.ok });
    })
    .catch(err => {
      sendResponse({ error: err.message || String(err) });
    });
    return true; // Keep message channel open for async response
  }

  return true;
});
