import React from 'react';

export const StatusBadge = ({ isOnline, onToggle }) => {
  return (
    <div
      onClick={onToggle}
      style={{
        position: 'fixed',
        top: '20px',
        right: '20px',
        padding: '8px 16px',
        backgroundColor: 'rgba(17, 24, 39, 0.88)',
        backdropFilter: 'blur(8px)',
        borderRadius: '9999px',
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
        color: 'white',
        fontFamily: "'Segoe UI', sans-serif",
        fontSize: '14px',
        fontWeight: '500',
        zIndex: 10001,
        boxShadow: '0 4px 24px rgba(0,0,0,0.4)',
        cursor: 'pointer',
        userSelect: 'none',
        border: '1px solid rgba(255,255,255,0.1)',
        transition: 'opacity 0.2s',
      }}
    >
      <div
        style={{
          width: '10px',
          height: '10px',
          borderRadius: '50%',
          backgroundColor: isOnline ? '#22c55e' : '#ef4444',
          boxShadow: isOnline ? '0 0 8px #22c55e' : '0 0 8px #ef4444',
        }}
      />
      {isOnline ? 'Engine Online' : 'Engine Offline'}
    </div>
  );
};

