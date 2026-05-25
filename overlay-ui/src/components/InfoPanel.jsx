import React from 'react';

const CLASS_COLORS = {
  'Brilliant': '#a855f7',
  'Best':      '#22c55e',
  'Good':      '#84cc16',
  'Inaccuracy':'#eab308',
  'Mistake':   '#f97316',
  'Blunder':   '#ef4444',
};

const CLASS_LABELS = {
  'Brilliant': '✨ Brilliant',
  'Best':      '✅ Best',
  'Good':      '👍 Good',
  'Inaccuracy':'⚠️ Inaccuracy',
  'Mistake':   '❌ Mistake',
  'Blunder':   '💀 Blunder',
};

export const InfoPanel = ({ analysis, isOnline }) => {
  return (
    <div style={{
      position: 'fixed',
      bottom: '20px',
      right: '20px',
      width: '280px',
      backgroundColor: 'rgba(15, 20, 30, 0.92)',
      backdropFilter: 'blur(12px)',
      borderRadius: '14px',
      border: '1px solid rgba(255,255,255,0.1)',
      padding: '16px',
      fontFamily: "'Segoe UI', sans-serif",
      color: 'white',
      boxShadow: '0 20px 60px rgba(0,0,0,0.5)',
      zIndex: 10000,
    }}>
      <div style={{ fontSize: '12px', opacity: 0.5, marginBottom: '8px', textTransform: 'uppercase', letterSpacing: '1px' }}>
        Chess Assistant
      </div>

      {!isOnline && (
        <div style={{ color: '#f97316', fontSize: '13px' }}>
          ⚠️ Backend offline — start Brain Backend first
        </div>
      )}

      {isOnline && !analysis && (
        <div style={{ color: '#94a3b8', fontSize: '13px' }}>
          Waiting for board position...
        </div>
      )}

      {analysis && (
        <>
          {/* Evaluation */}
          <div style={{ marginBottom: '12px' }}>
            <div style={{ fontSize: '11px', opacity: 0.5, marginBottom: '4px' }}>Evaluation</div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <span style={{ fontSize: '24px', fontWeight: 'bold', color: '#f8fafc' }}>
                {analysis.evaluation >= 0 ? '+' : ''}{(analysis.evaluation / 100).toFixed(2)}
              </span>
              {analysis.classification && (
                <span style={{
                  padding: '2px 8px',
                  borderRadius: '9999px',
                  fontSize: '11px',
                  fontWeight: '600',
                  backgroundColor: CLASS_COLORS[analysis.classification] + '33',
                  color: CLASS_COLORS[analysis.classification],
                  border: `1px solid ${CLASS_COLORS[analysis.classification]}55`,
                }}>
                  {CLASS_LABELS[analysis.classification] || analysis.classification}
                </span>
              )}
            </div>
          </div>

          {/* Best moves */}
          {analysis.bestMoves && analysis.bestMoves.length > 0 && (
            <div>
              <div style={{ fontSize: '11px', opacity: 0.5, marginBottom: '6px' }}>Top Moves</div>
              {analysis.bestMoves.slice(0, 3).map((m, i) => (
                <div key={i} style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  padding: '6px 8px',
                  borderRadius: '8px',
                  backgroundColor: i === 0 ? 'rgba(34,197,94,0.12)' : 'rgba(255,255,255,0.04)',
                  marginBottom: '4px',
                  border: i === 0 ? '1px solid rgba(34,197,94,0.25)' : '1px solid transparent',
                }}>
                  <span style={{ fontSize: '15px', fontWeight: '600', fontFamily: 'monospace', color: i === 0 ? '#86efac' : '#cbd5e1' }}>
                    {m.move}
                  </span>
                  <span style={{ fontSize: '12px', color: '#64748b' }}>
                    {m.score >= 0 ? '+' : ''}{(m.score / 100).toFixed(2)}
                  </span>
                </div>
              ))}
            </div>
          )}

          {/* FEN snippet */}
          <div style={{ marginTop: '10px', fontSize: '9px', opacity: 0.3, wordBreak: 'break-all', fontFamily: 'monospace' }}>
            {analysis.fen}
          </div>
        </>
      )}

      <div style={{ marginTop: '10px', fontSize: '10px', opacity: 0.3, textAlign: 'center' }}>
        🌐 Open overlay-ui alongside Chess.com
      </div>
    </div>
  );
};
