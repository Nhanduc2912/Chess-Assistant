import React from 'react';
import { useCoordinateMap } from '../hooks/useCoordinateMap';

// Color mapping based on EvaluationService.cs
const getClassColor = (classification) => {
  switch (classification) {
    case 'Blunder': return 'rgba(239, 68, 68, 0.8)'; // Red-500
    case 'Mistake': return 'rgba(249, 115, 22, 0.8)'; // Orange-500
    case 'Inaccuracy': return 'rgba(234, 179, 8, 0.8)'; // Yellow-500
    case 'Good': return 'rgba(132, 204, 22, 0.8)'; // Lime-500
    case 'Best': return 'rgba(34, 197, 94, 0.8)'; // Green-500
    case 'Brilliant': return 'rgba(168, 85, 247, 0.8)'; // Purple-500
    default: return 'rgba(156, 163, 175, 0.8)'; // Gray-400
  }
};

const SingleArrow = ({ moveInfo, bbox, rank, classification, isWhiteBottom }) => {
  const coords = useCoordinateMap(moveInfo.move, bbox, isWhiteBottom);
  
  if (!coords) return null;
  
  const { x1, y1, x2, y2 } = coords;
  
  // Opacity decreases for lower ranked moves
  const opacities = [1.0, 0.6, 0.3];
  const opacity = opacities[rank] || 0.1;
  
  const color = getClassColor(classification);
  const strokeWidth = 14;

  // Arrowhead ID specific to this arrow's color
  const markerId = `arrowhead-${rank}`;

  return (
    <>
      <defs>
        <marker
          id={markerId}
          markerWidth="10"
          markerHeight="7"
          refX="9"
          refY="3.5"
          orient="auto"
          markerUnits="strokeWidth"
        >
          <polygon points="0 0, 10 3.5, 0 7" fill={color} />
        </marker>
      </defs>
      <line
        x1={x1}
        y1={y1}
        x2={x2}
        y2={y2}
        stroke={color}
        strokeWidth={strokeWidth}
        strokeOpacity={opacity}
        markerEnd={`url(#${markerId})`}
        style={{
          transition: 'all 0.15s ease-in-out',
          pointerEvents: 'none'
        }}
      />
    </>
  );
};

export const Arrows = ({ analysis }) => {
  if (!analysis || !analysis.bestMoves || analysis.bestMoves.length === 0 || !analysis.bbox || !analysis.fen) {
    return null;
  }

  const { bestMoves, bbox, classification, isWhiteBottom, fen } = analysis;
  
  // Check whose turn it is
  const fenParts = fen.split(' ');
  const activeColor = fenParts.length > 1 ? fenParts[1] : 'w';
  
  // Only show arrows if it's the user's turn
  const isUserTurn = (isWhiteBottom && activeColor === 'w') || (!isWhiteBottom && activeColor === 'b');
  if (!isUserTurn) {
    return null;
  }

  return (
    <svg 
      style={{ 
        position: 'fixed', 
        top: 0, 
        left: 0, 
        width: '100vw', 
        height: '100vh', 
        pointerEvents: 'none',
        zIndex: 9999
      }}
    >
      {bestMoves.map((moveInfo, index) => (
        <SingleArrow 
          key={`${moveInfo.move}-${index}`} 
          moveInfo={moveInfo} 
          bbox={bbox} 
          rank={index}
          classification={index === 0 ? classification : 'Unknown'}
          isWhiteBottom={isWhiteBottom}
        />
      ))}
    </svg>
  );
};
