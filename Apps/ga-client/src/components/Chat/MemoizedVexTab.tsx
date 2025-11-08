import React, { memo } from 'react';

interface MemoizedVexTabProps {
  content: string;
}

/**
 * Memoized VexTab component to prevent unnecessary re-renders
 * VexTab rendering can be expensive, so we memoize it
 */
const MemoizedVexTab: React.FC<MemoizedVexTabProps> = memo(
  ({ content }) => {
    return (
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
    );
  },
  // Custom comparison function - only re-render if content changes
  (prevProps, nextProps) => prevProps.content === nextProps.content
);

MemoizedVexTab.displayName = 'MemoizedVexTab';

export default MemoizedVexTab;

