// src/components/PrimeRadiant/IcicleDrawer.tsx
// Bottom drawer with drag-to-resize handle and split pane layout.
// Left pane: icicle navigator placeholder (Unit 3).
// Right pane: file content viewer placeholder (Unit 4).

import React, { useCallback, useEffect, useRef, useState } from 'react';
import type { GovernanceGraph } from './types';

const DEFAULT_OPEN_HEIGHT = 300;
const MIN_HEIGHT = 150;
const MAX_HEIGHT_VH = 0.6; // 60vh

export interface IcicleDrawerProps {
  graphData: GovernanceGraph | null;
}

export const IcicleDrawer: React.FC<IcicleDrawerProps> = ({ graphData }) => {
  const [drawerHeight, setDrawerHeight] = useState(0);
  const [selectedFile, setSelectedFile] = useState<string | null>(null);
  const [fileContent, setFileContent] = useState<string | null>(null);
  const [fileMediaType, setFileMediaType] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const isDraggingRef = useRef(false);
  const startYRef = useRef(0);
  const startHeightRef = useRef(0);

  // Suppress unused-variable warnings for state that will be used in Units 3-4
  void selectedFile;
  void fileContent;
  void fileMediaType;
  void loading;
  void setSelectedFile;
  void setFileContent;
  void setFileMediaType;
  void setLoading;
  void graphData;

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
      // Dragging up (negative deltaY) increases height
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

  const isOpen = drawerHeight > 0;

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
          <div className="icicle-drawer__icicle">
            Icicle view loading...
          </div>
          <div className="icicle-drawer__content">
            Select a file to view its content
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
