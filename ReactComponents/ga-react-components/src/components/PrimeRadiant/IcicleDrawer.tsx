// src/components/PrimeRadiant/IcicleDrawer.tsx
// Bottom drawer with drag-to-resize handle and split pane layout.
// Left pane: hierarchical tree navigator grouped by governance type (Unit 3).
// Right pane: file content viewer with markdown rendering (Unit 4).

import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import type { GovernanceGraph, GovernanceNode, GovernanceNodeType } from './types';

const DEFAULT_OPEN_HEIGHT = 300;
const MIN_HEIGHT = 150;
const MAX_HEIGHT_VH = 0.6; // 60vh

// ---------------------------------------------------------------------------
// Type group configuration — color-coded headers for the tree navigator
// ---------------------------------------------------------------------------
interface TypeGroupConfig {
  label: string;
  color: string;
}

const TYPE_GROUP_CONFIG: Record<GovernanceNodeType, TypeGroupConfig> = {
  constitution: { label: 'Constitutions', color: '#FFD700' },  // gold
  policy:       { label: 'Policies',      color: '#4A9EFF' },  // blue
  persona:      { label: 'Personas',      color: '#B07AFF' },  // purple
  schema:       { label: 'Schemas',       color: '#33CC66' },  // green
  pipeline:     { label: 'Pipelines',     color: '#00CED1' },  // cyan
  test:         { label: 'Tests',         color: '#FF8C42' },  // orange
  department:   { label: 'Departments',   color: '#FF69B4' },  // pink
  ixql:         { label: 'IXQL',          color: '#20B2AA' },  // teal
};

// ---------------------------------------------------------------------------
// Grouped hierarchy: type -> nodes
// ---------------------------------------------------------------------------
interface TypeGroup {
  type: GovernanceNodeType;
  config: TypeGroupConfig;
  nodes: GovernanceNode[];
}

function buildTypeGroups(graphData: GovernanceGraph | null): TypeGroup[] {
  if (!graphData?.nodes?.length) return [];

  const grouped = new Map<GovernanceNodeType, GovernanceNode[]>();
  for (const node of graphData.nodes) {
    const existing = grouped.get(node.type);
    if (existing) {
      existing.push(node);
    } else {
      grouped.set(node.type, [node]);
    }
  }

  // Produce groups in the order defined by TYPE_GROUP_CONFIG
  const groups: TypeGroup[] = [];
  for (const [type, config] of Object.entries(TYPE_GROUP_CONFIG)) {
    const nodes = grouped.get(type as GovernanceNodeType);
    if (nodes?.length) {
      groups.push({
        type: type as GovernanceNodeType,
        config,
        nodes: nodes.sort((a, b) => a.name.localeCompare(b.name)),
      });
    }
  }
  return groups;
}

// ---------------------------------------------------------------------------
// File content fetcher
// ---------------------------------------------------------------------------
interface FileContentResponse {
  content: string;
  filePath: string;
  mediaType: string;
}

async function fetchFileContent(filePath: string): Promise<FileContentResponse> {
  const baseUrl = window.location.origin;
  const url = `${baseUrl}/api/governance/file-content?filePath=${encodeURIComponent(filePath)}`;
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(
      response.status === 404
        ? `File not found: ${filePath}`
        : `Failed to load file (${response.status})`
    );
  }
  return response.json() as Promise<FileContentResponse>;
}

// ---------------------------------------------------------------------------
// Markdown custom components — styled for dark theme
// ---------------------------------------------------------------------------
const markdownComponents = {
  h1: ({ children }: { children?: React.ReactNode }) => (
    <h1 className="icicle-drawer__content-h1">{children}</h1>
  ),
  h2: ({ children }: { children?: React.ReactNode }) => (
    <h2 className="icicle-drawer__content-h2">{children}</h2>
  ),
  h3: ({ children }: { children?: React.ReactNode }) => (
    <h3 className="icicle-drawer__content-h3">{children}</h3>
  ),
  h4: ({ children }: { children?: React.ReactNode }) => (
    <h4 className="icicle-drawer__content-h4">{children}</h4>
  ),
  p: ({ children }: { children?: React.ReactNode }) => (
    <p style={{ margin: '6px 0', lineHeight: 1.7 }}>{children}</p>
  ),
  a: ({ href, children }: { href?: string; children?: React.ReactNode }) => (
    <a
      href={href}
      style={{ color: '#58a6ff' }}
      target="_blank"
      rel="noopener noreferrer"
    >
      {children}
    </a>
  ),
  strong: ({ children }: { children?: React.ReactNode }) => (
    <strong style={{ color: '#e6edf3' }}>{children}</strong>
  ),
  code: ({ children, className }: { children?: React.ReactNode; className?: string }) => {
    // Fenced code blocks get a className like "language-xxx"
    if (className) {
      return (
        <pre className="icicle-drawer__code-block">
          <code>{children}</code>
        </pre>
      );
    }
    // Inline code
    return (
      <code className="icicle-drawer__inline-code">{children}</code>
    );
  },
  pre: ({ children }: { children?: React.ReactNode }) => (
    <>{children}</>
  ),
  table: ({ children }: { children?: React.ReactNode }) => (
    <table className="icicle-drawer__table">{children}</table>
  ),
  th: ({ children }: { children?: React.ReactNode }) => (
    <th className="icicle-drawer__th">{children}</th>
  ),
  td: ({ children }: { children?: React.ReactNode }) => (
    <td className="icicle-drawer__td">{children}</td>
  ),
  li: ({ children }: { children?: React.ReactNode }) => (
    <li style={{ margin: '2px 0' }}>{children}</li>
  ),
  blockquote: ({ children }: { children?: React.ReactNode }) => (
    <blockquote className="icicle-drawer__blockquote">{children}</blockquote>
  ),
};

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export interface IcicleDrawerProps {
  graphData: GovernanceGraph | null;
}

export const IcicleDrawer: React.FC<IcicleDrawerProps> = ({ graphData }) => {
  const [drawerHeight, setDrawerHeight] = useState(0);
  const [selectedFile, setSelectedFile] = useState<string | null>(null);
  const [fileContent, setFileContent] = useState<string | null>(null);
  const [fileMediaType, setFileMediaType] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [expandedGroups, setExpandedGroups] = useState<Set<string>>(new Set());

  const isDraggingRef = useRef(false);
  const startYRef = useRef(0);
  const startHeightRef = useRef(0);

  // Build grouped hierarchy from flat governance nodes
  const typeGroups = useMemo(() => buildTypeGroups(graphData), [graphData]);

  // Expand all groups by default when data arrives
  useEffect(() => {
    if (typeGroups.length > 0 && expandedGroups.size === 0) {
      setExpandedGroups(new Set(typeGroups.map(g => g.type)));
    }
  }, [typeGroups]); // eslint-disable-line react-hooks/exhaustive-deps

  const clampHeight = useCallback((h: number): number => {
    const maxPx = window.innerHeight * MAX_HEIGHT_VH;
    if (h < MIN_HEIGHT) return 0; // snap closed if below minimum
    return Math.min(h, maxPx);
  }, []);

  const handleMouseDown = useCallback((e: React.MouseEvent) => {
    e.preventDefault();
    isDraggingRef.current = true;
    startYRef.current = e.clientY;
    startHeightRef.current = drawerHeight;
    document.body.style.cursor = 'ns-resize';
    document.body.style.userSelect = 'none';
  }, [drawerHeight]);

  const handleTouchStart = useCallback((e: React.TouchEvent) => {
    const touch = e.touches[0];
    isDraggingRef.current = true;
    startYRef.current = touch.clientY;
    startHeightRef.current = drawerHeight;
  }, [drawerHeight]);

  useEffect(() => {
    const handleMouseMove = (e: MouseEvent) => {
      if (!isDraggingRef.current) return;
      const deltaY = startYRef.current - e.clientY;
      const newHeight = startHeightRef.current + deltaY;
      setDrawerHeight(clampHeight(newHeight));
    };

    const handleMouseUp = () => {
      if (!isDraggingRef.current) return;
      isDraggingRef.current = false;
      document.body.style.cursor = '';
      document.body.style.userSelect = '';
    };

    const handleTouchMove = (e: TouchEvent) => {
      if (!isDraggingRef.current) return;
      const touch = e.touches[0];
      const deltaY = startYRef.current - touch.clientY;
      const newHeight = startHeightRef.current + deltaY;
      setDrawerHeight(clampHeight(newHeight));
    };

    const handleTouchEnd = () => {
      isDraggingRef.current = false;
    };

    document.addEventListener('mousemove', handleMouseMove);
    document.addEventListener('mouseup', handleMouseUp);
    document.addEventListener('touchmove', handleTouchMove, { passive: true });
    document.addEventListener('touchend', handleTouchEnd);

    return () => {
      document.removeEventListener('mousemove', handleMouseMove);
      document.removeEventListener('mouseup', handleMouseUp);
      document.removeEventListener('touchmove', handleTouchMove);
      document.removeEventListener('touchend', handleTouchEnd);
    };
  }, [clampHeight]);

  const handleDoubleClick = useCallback(() => {
    setDrawerHeight(prev => (prev === 0 ? DEFAULT_OPEN_HEIGHT : 0));
  }, []);

  // Toggle a type group open/closed
  const toggleGroup = useCallback((type: string) => {
    setExpandedGroups(prev => {
      const next = new Set(prev);
      if (next.has(type)) {
        next.delete(type);
      } else {
        next.add(type);
      }
      return next;
    });
  }, []);

  // Handle file selection — fetch content from the API
  const handleFileSelect = useCallback(async (node: GovernanceNode) => {
    const filePath = node.filePath;
    if (!filePath) return;

    // If same file is already selected, deselect
    if (selectedFile === filePath) {
      setSelectedFile(null);
      setFileContent(null);
      setFileMediaType(null);
      setError(null);
      return;
    }

    setSelectedFile(filePath);
    setFileContent(null);
    setFileMediaType(null);
    setError(null);
    setLoading(true);

    try {
      const result = await fetchFileContent(filePath);
      setFileContent(result.content);
      setFileMediaType(result.mediaType);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load file');
    } finally {
      setLoading(false);
    }
  }, [selectedFile]);

  const isOpen = drawerHeight > 0;

  // ── Render content viewer ──
  const renderContent = () => {
    if (loading) {
      return <div className="icicle-drawer__placeholder">Loading...</div>;
    }
    if (error) {
      return <div className="icicle-drawer__placeholder icicle-drawer__error">{error}</div>;
    }
    if (!selectedFile || fileContent === null) {
      return <div className="icicle-drawer__placeholder">Select a file from the navigator</div>;
    }

    // Markdown rendering
    if (fileMediaType === 'text/markdown') {
      return (
        <div className="icicle-drawer__markdown">
          <ReactMarkdown
            remarkPlugins={[remarkGfm]}
            components={markdownComponents}
          >
            {fileContent}
          </ReactMarkdown>
        </div>
      );
    }

    // Code / structured data rendering
    return (
      <pre className="icicle-drawer__code-block">
        <code>{fileContent}</code>
      </pre>
    );
  };

  return (
    <div
      className={`icicle-drawer${isOpen ? ' icicle-drawer--open' : ''}`}
      style={{ height: drawerHeight + 8 /* 8px for the handle */ }}
    >
      {/* Drag handle */}
      <div
        className="icicle-drawer__handle"
        onMouseDown={handleMouseDown}
        onTouchStart={handleTouchStart}
        onDoubleClick={handleDoubleClick}
        role="separator"
        aria-orientation="horizontal"
        aria-label="Resize drawer"
        tabIndex={0}
      />

      {/* Drawer body — only rendered when open */}
      {isOpen && (
        <div className="icicle-drawer__body" style={{ height: drawerHeight }}>
          {/* Left pane: tree navigator */}
          <div className="icicle-drawer__icicle">
            {typeGroups.length === 0 ? (
              <div className="icicle-drawer__placeholder">No governance data</div>
            ) : (
              <div className="icicle-drawer__nav-tree">
                {typeGroups.map(group => {
                  const isExpanded = expandedGroups.has(group.type);
                  return (
                    <div key={group.type} className="icicle-drawer__nav-group">
                      <button
                        className="icicle-drawer__nav-group-header"
                        onClick={() => toggleGroup(group.type)}
                        style={{ borderLeftColor: group.config.color }}
                      >
                        <span
                          className="icicle-drawer__nav-chevron"
                          style={{ transform: isExpanded ? 'rotate(90deg)' : 'rotate(0deg)' }}
                        >
                          &#9654;
                        </span>
                        <span
                          className="icicle-drawer__nav-group-label"
                          style={{ color: group.config.color }}
                        >
                          {group.config.label}
                        </span>
                        <span className="icicle-drawer__nav-group-count">
                          {group.nodes.length}
                        </span>
                      </button>
                      {isExpanded && (
                        <div className="icicle-drawer__nav-group-items">
                          {group.nodes.map(node => (
                            <button
                              key={node.id}
                              className={`icicle-drawer__nav-item${
                                selectedFile === node.filePath
                                  ? ' icicle-drawer__nav-item--selected'
                                  : ''
                              }`}
                              onClick={() => handleFileSelect(node)}
                              title={node.filePath ?? node.name}
                            >
                              <span className="icicle-drawer__nav-item-name">
                                {node.name}
                              </span>
                            </button>
                          ))}
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            )}
          </div>

          {/* Right pane: file content viewer */}
          <div className="icicle-drawer__content">
            {renderContent()}
          </div>
        </div>
      )}

      {/* Mobile close button */}
      {isOpen && (
        <button
          className="icicle-drawer__close-btn"
          onClick={() => setDrawerHeight(0)}
          aria-label="Close drawer"
        >
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <line x1="18" y1="6" x2="6" y2="18" />
            <line x1="6" y1="6" x2="18" y2="18" />
          </svg>
        </button>
      )}
    </div>
  );
};
