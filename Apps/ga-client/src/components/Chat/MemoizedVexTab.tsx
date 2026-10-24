import React, { memo, useEffect, useMemo, useRef, useState } from 'react';
import { parseChatVexTab } from './vextabChords';

interface MemoizedVexTabProps {
  content: string;
}

// VexFlow lives in ga-react-components, as the other components ga-client borrows from it;
// loaded on the first block drawn.
const loadRenderer = () => import('../../../../../ReactComponents/ga-react-components/src/components/TabChordsRenderer');

type RenderState = 'pending' | 'drawn' | 'text';

/**
 * Draws a `vextab` block as tab with VexFlow. The text is shown instead when the block can't be
 * read (see parseChatVexTab) or drawing fails, and while the renderer loads. `data-renderer`
 * says which one the reader sees: `vexflow` or `text`.
 */
const MemoizedVexTab: React.FC<MemoizedVexTabProps> = memo(
  ({ content }) => {
    const parsed = useMemo(() => parseChatVexTab(content), [content]);
    const drawing = useRef<HTMLDivElement>(null);
    const [state, setState] = useState<RenderState>(parsed ? 'pending' : 'text');

    useEffect(() => {
      if (!parsed) {
        setState('text');
        return;
      }
      let cancelled = false;
      setState('pending');
      loadRenderer()
        .then(({ renderTabChords }) => {
          if (cancelled || !drawing.current) return;
          renderTabChords(drawing.current, parsed.chords);
          setState('drawn');
        })
        .catch((error: unknown) => {
          if (cancelled) return;
          console.warn('Could not draw vextab block:', error);
          if (drawing.current) drawing.current.innerHTML = '';
          setState('text');
        });
      return () => {
        cancelled = true;
      };
    }, [parsed]);

    return (
      <div className="vextab-block" data-renderer={state === 'drawn' ? 'vexflow' : 'text'}>
        <div
          ref={drawing}
          role={state === 'drawn' ? 'img' : undefined}
          aria-label={state === 'drawn' ? 'Guitar tab' : undefined}
          style={state === 'drawn' ? { backgroundColor: '#fff', borderRadius: 8, overflowX: 'auto' } : undefined}
        />
        {state !== 'drawn' && (
          <pre
            style={{
              backgroundColor: 'rgba(255,255,255,0.08)',
              padding: '12px',
              borderRadius: 8,
              overflowX: 'auto',
            }}
          >
            {content}
          </pre>
        )}
      </div>
    );
  },
  // Custom comparison function - only re-render if content changes
  (prevProps, nextProps) => prevProps.content === nextProps.content
);

MemoizedVexTab.displayName = 'MemoizedVexTab';

export default MemoizedVexTab;
