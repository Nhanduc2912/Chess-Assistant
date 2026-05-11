import React from 'react';

export const EvalBar = ({ evaluation, bbox, fen, isWhiteBottom }) => {
  if (evaluation === undefined || !bbox || !fen) return null;

  const activeColor = fen.split(' ').length > 1 ? fen.split(' ')[1] : 'w';
  
  // evaluation is in centipawns relative to active color.
  // Convert to pawns from white's perspective
  let whiteScore = (activeColor === 'w' ? evaluation : -evaluation) / 100.0;
  
  // Calculate height percentage (cap at +/- 10 pawns)
  const clampedScore = Math.max(-10, Math.min(10, whiteScore));
  const whitePercentage = 50 + (clampedScore * 5);

  const barWidth = 20;
  const margin = 10;
  const left = bbox.x - barWidth - margin;
  
  if (left < 0) return null;

  const topColor = isWhiteBottom ? '#404040' : '#e5e5e5';
  const bottomColor = isWhiteBottom ? '#e5e5e5' : '#404040';
  const topPercentage = isWhiteBottom ? 100 - whitePercentage : whitePercentage;
  const bottomPercentage = isWhiteBottom ? whitePercentage : 100 - whitePercentage;

  // The text shown is usually absolute advantage
  const displayScore = Math.abs(whiteScore).toFixed(1);
  // Is bottom player winning?
  const bottomWinning = isWhiteBottom ? whiteScore >= 0 : whiteScore <= 0;

  return (
    <div 
      style={{
        position: 'fixed',
        left: `${left}px`,
        top: `${bbox.y}px`,
        width: `${barWidth}px`,
        height: `${bbox.h}px`,
        backgroundColor: '#333',
        borderRadius: '4px',
        overflow: 'hidden',
        display: 'flex',
        flexDirection: 'column',
        boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.5)',
        zIndex: 9999
      }}
    >
      {/* Top portion */}
      <div 
        style={{
          width: '100%',
          height: `${topPercentage}%`,
          backgroundColor: topColor,
          transition: 'height 0.3s ease-out',
          display: 'flex',
          alignItems: 'flex-end',
          justifyContent: 'center',
          paddingBottom: '4px'
        }}
      >
        {!bottomWinning && (
          <span style={{
            color: isWhiteBottom ? '#e5e5e5' : '#333',
            fontSize: '10px',
            fontWeight: 'bold',
            fontFamily: 'monospace'
          }}>
            {displayScore}
          </span>
        )}
      </div>
      
      {/* Bottom portion */}
      <div 
        style={{
          width: '100%',
          height: `${bottomPercentage}%`,
          backgroundColor: bottomColor,
          transition: 'height 0.3s ease-out',
          display: 'flex',
          alignItems: 'flex-start',
          justifyContent: 'center',
          paddingTop: '4px'
        }}
      >
        {bottomWinning && (
          <span style={{
            color: isWhiteBottom ? '#333' : '#e5e5e5',
            fontSize: '10px',
            fontWeight: 'bold',
            fontFamily: 'monospace'
          }}>
            {displayScore}
          </span>
        )}
      </div>
    </div>
  );
};
