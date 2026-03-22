// MarkdownCard — Renders --- markdown comments as annotation cards

import React from 'react';
import ReactMarkdown from 'react-markdown';

interface MarkdownCardProps {
  content: string;
  bindingName: string;
}

export const MarkdownCard: React.FC<MarkdownCardProps> = ({ content, bindingName }) => {
  return (
    <div
      style={{
        background: '#161b22',
        border: '1px solid #30363d',
        borderLeft: '3px solid #7289da',
        borderRadius: 6,
        padding: '10px 14px',
        fontSize: 12,
        color: '#c9d1d9',
        fontFamily: '-apple-system, BlinkMacSystemFont, sans-serif',
        lineHeight: 1.6,
        maxWidth: 400,
      }}
    >
      <div
        style={{
          fontSize: 10,
          color: '#7289da',
          marginBottom: 6,
          textTransform: 'uppercase',
          letterSpacing: 0.5,
        }}
      >
        {bindingName}
      </div>
      <ReactMarkdown
        components={{
          h1: ({ children }) => (
            <h4 style={{ color: '#e6edf3', margin: '8px 0 4px' }}>{children}</h4>
          ),
          h2: ({ children }) => (
            <h5 style={{ color: '#e6edf3', margin: '6px 0 4px' }}>{children}</h5>
          ),
          p: ({ children }) => <p style={{ margin: '4px 0' }}>{children}</p>,
          code: ({ children }) => (
            <code
              style={{
                background: '#0d1117',
                padding: '1px 4px',
                borderRadius: 3,
                fontSize: 11,
                fontFamily: "'JetBrains Mono', monospace",
              }}
            >
              {children}
            </code>
          ),
          a: ({ href, children }) => (
            <a
              href={href}
              style={{ color: '#58a6ff' }}
              target="_blank"
              rel="noopener noreferrer"
            >
              {children}
            </a>
          ),
          strong: ({ children }) => (
            <strong style={{ color: '#e6edf3' }}>{children}</strong>
          ),
        }}
      >
        {content}
      </ReactMarkdown>
    </div>
  );
};
