import React, { Suspense, useEffect, useState } from 'react';
import { Container } from '@mui/material';
import CastButton from '../components/Common/CastButton';

// Use the new force-directed Prime Radiant
const ForceRadiant = React.lazy(() =>
  import('../components/PrimeRadiant').then(mod => ({ default: mod.ForceRadiant }))
);

type GraphSource = 'governance' | 'assumption';

const PrimeRadiantTest: React.FC = () => {
  console.log('[PrimeRadiantTest] Rendering ForceRadiant...');

  // Which graph to render: governance (default) or the IX temporal assumption
  // graph (served by the Vite /dev-data/assumption middleware).
  const [source, setSource] = useState<GraphSource>('governance');
  const liveDataUrl = source === 'assumption' ? '/dev-data/assumption' : '/api/governance';
  const liveHubUrl = source === 'assumption' ? undefined : '/hubs/governance';

  // Prevent body scrolling while Prime Radiant is mounted
  useEffect(() => {
    const prev = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    document.documentElement.style.overflow = 'hidden';
    return () => {
      document.body.style.overflow = prev;
      document.documentElement.style.overflow = '';
    };
  }, []);

  return (
    <Container maxWidth={false} disableGutters sx={{ height: '100%', overflow: 'hidden', position: 'relative' }}>
      <CastButton right={64} />
      <div style={{ position: 'absolute', top: 12, left: 12, zIndex: 20, display: 'flex', gap: 8 }}>
        {(['governance', 'assumption'] as const).map((s) => (
          <button
            key={s}
            onClick={() => setSource(s)}
            style={{
              padding: '6px 12px',
              borderRadius: 6,
              cursor: 'pointer',
              fontFamily: 'monospace',
              fontSize: 13,
              background: source === s ? '#FFD700' : 'rgba(0,0,0,0.6)',
              color: source === s ? '#000' : '#FFD700',
              border: '1px solid #FFD700',
            }}
          >
            {s === 'governance' ? 'Governance' : 'Assumptions'}
          </button>
        ))}
      </div>
      <Suspense fallback={
        <div style={{
          width: '100%',
          height: '100%',
          background: '#000008',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          color: '#FFD700',
          fontFamily: 'monospace',
          fontSize: 18,
        }}>
          Loading Prime Radiant...
        </div>
      }>
        <ForceRadiant
          key={source}
          liveDataUrl={liveDataUrl}
          liveHubUrl={liveHubUrl}
          onNodeSelect={(node) => {
            if (node) {
              console.log('[PrimeRadiant] Selected:', node.name, node.type);
            } else {
              console.log('[PrimeRadiant] Selection cleared');
            }
          }}
        />
      </Suspense>
    </Container>
  );
};

export default PrimeRadiantTest;
