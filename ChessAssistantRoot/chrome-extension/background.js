/**
 * Background Service Worker v2
 * Handles badge updates and message routing.
 */

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

  return true;
});
