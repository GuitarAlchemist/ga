// src/components/PrimeRadiant/IxqlCommandInput.tsx
// Minimal IXql command palette for Prime Radiant visualization control

import React, { useState, useRef, useEffect, useCallback } from 'react';
import { parseIxqlCommand, type IxqlParseResult } from './IxqlControlParser';

interface IxqlCommandInputProps {
  onCommand: (result: IxqlParseResult) => void;
}

export const IxqlCommandInput: React.FC<IxqlCommandInputProps> = ({ onCommand }) => {
  const [visible, setVisible] = useState(false);
  const [value, setValue] = useState('');
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const toggle = useCallback(() => {
    setVisible(v => {
      if (!v) {
        // Opening — focus after render
        setTimeout(() => inputRef.current?.focus(), 50);
      }
      return !v;
    });
    setError(null);
  }, []);

  // Keyboard shortcut: backtick or Ctrl+I
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.key === '`' && !e.ctrlKey && !e.metaKey) {
        // Don't toggle if user is typing in another input
        const active = document.activeElement;
        if (active && (active.tagName === 'INPUT' || active.tagName === 'TEXTAREA')) return;
        e.preventDefault();
        toggle();
      }
      if (e.key === 'i' && (e.ctrlKey || e.metaKey)) {
        e.preventDefault();
        toggle();
      }
      if (e.key === 'Escape' && visible) {
        setVisible(false);
        setError(null);
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [toggle, visible]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!value.trim()) return;

    const result = parseIxqlCommand(value);
    if (result.ok) {
      onCommand(result);
      setError(null);
      if (result.command?.type === 'reset') {
        setValue('');
      }
    } else {
      setError(result.error ?? 'Parse error');
    }
  };

  if (!visible) return null;

  return (
    <div className="prime-radiant__ixql-input">
      <form onSubmit={handleSubmit} style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
        <span style={{ color: '#FFD700', fontSize: 11, fontWeight: 600 }}>IXql</span>
        <input
          ref={inputRef}
          type="text"
          value={value}
          onChange={e => { setValue(e.target.value); setError(null); }}
          placeholder="SELECT nodes WHERE type='policy' SET glow=red"
          spellCheck={false}
          autoComplete="off"
          style={{
            flex: 1,
            background: 'transparent',
            border: 'none',
            outline: 'none',
            color: '#e6edf3',
            fontFamily: "'JetBrains Mono', monospace",
            fontSize: 11,
          }}
        />
        <button
          type="submit"
          style={{
            background: '#FFD70022',
            border: '1px solid #FFD70044',
            borderRadius: 4,
            color: '#FFD700',
            fontFamily: "'JetBrains Mono', monospace",
            fontSize: 10,
            padding: '2px 8px',
            cursor: 'pointer',
          }}
        >
          Run
        </button>
      </form>
      {error && (
        <div style={{ color: '#FF4444', fontSize: 10, marginTop: 2, paddingLeft: 32 }}>
          {error}
        </div>
      )}
    </div>
  );
};
