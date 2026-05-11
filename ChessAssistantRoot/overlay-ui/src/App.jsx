import React, { useState } from 'react';
import { useSignalR } from './hooks/useSignalR';
import { Arrows } from './components/Arrows';
import { EvalBar } from './components/EvalBar';
import { StatusBadge } from './components/StatusBadge';
import { InfoPanel } from './components/InfoPanel';

const HUB_URL = "http://localhost:5000/chessHub";

function App() {
  const { isOnline, latestAnalysis } = useSignalR(HUB_URL);
  const [showPanel, setShowPanel] = useState(true);

  return (
    <>
      {/* Status badge — always visible, top-right */}
      <StatusBadge isOnline={isOnline} onToggle={() => setShowPanel(v => !v)} />

      {/* Info panel — shows best moves & eval score */}
      {showPanel && (
        <InfoPanel analysis={latestAnalysis} isOnline={isOnline} />
      )}

      {/* SVG arrows — click-through overlay */}
      <Arrows analysis={latestAnalysis} />

      {/* Eval bar — click-through overlay */}
      <EvalBar
        evaluation={latestAnalysis?.evaluation}
        bbox={latestAnalysis?.bbox}
        fen={latestAnalysis?.fen}
        isWhiteBottom={latestAnalysis?.isWhiteBottom}
      />
    </>
  );
}

export default App;
