import React, { memo } from 'react';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { vscDarkPlus } from 'react-syntax-highlighter/dist/esm/styles/prism';

interface MemoizedCodeBlockProps {
  language: string;
  content: string;
}

/**
 * Memoized code block component to prevent unnecessary re-renders
 * Syntax highlighting can be expensive, so we memoize it
 */
const MemoizedCodeBlock: React.FC<MemoizedCodeBlockProps> = memo(
  ({ language, content }) => {
    return (
      <SyntaxHighlighter language={language} style={vscDarkPlus} PreTag="div">
        {content}
      </SyntaxHighlighter>
    );
  },
  // Custom comparison function - only re-render if language or content changes
  (prevProps, nextProps) =>
    prevProps.language === nextProps.language && prevProps.content === nextProps.content
);

MemoizedCodeBlock.displayName = 'MemoizedCodeBlock';

export default MemoizedCodeBlock;

