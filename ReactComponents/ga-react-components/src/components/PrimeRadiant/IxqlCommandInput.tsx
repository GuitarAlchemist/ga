// src/components/PrimeRadiant/IxqlCommandInput.tsx
// IXQL command palette with grammar-driven autocomplete

import React, { useState, useRef, useEffect, useCallback } from 'react';
import { parseIxqlCommand, type IxqlParseResult } from './IxqlControlParser';

// ---------------------------------------------------------------------------
// Grammar metadata (fetched from backend, cached)
// ---------------------------------------------------------------------------

interface GrammarMetadata {
  uiKeywords: string[];
  dataSources: string[];
  ioTools: string[];
  sectionCount: number;
}

let cachedGrammar: GrammarMetadata | null = null;

async function fetchGrammarMetadata(): Promise<GrammarMetadata> {
  if (cachedGrammar) return cachedGrammar;
  try {
    const res = await fetch('/api/governance/grammar');
    if (res.ok) {
      const data = await res.json();
      cachedGrammar = {
        uiKeywords: data.uiKeywords ?? [],
        dataSources: data.dataSources ?? [],
        ioTools: data.ioTools ?? [],
        sectionCount: data.sectionCount ?? 0,
      };
      return cachedGrammar;
    }
  } catch { /* fallback to static */ }
  // Fallback: static keywords if backend unavailable
  cachedGrammar = {
    uiKeywords: ['SELECT', 'RESET', 'CREATE', 'PANEL', 'VIZ', 'KIND', 'grid', 'force-graph',
      'FROM', 'SOURCE', 'WHERE', 'SET', 'PROJECT', 'PIPE', 'FILTER', 'SORT', 'LIMIT',
      'GROUP', 'BY', 'SHOW', 'beliefs', 'BIND', 'HEALTH', 'GOVERNED'],
    dataSources: ['governance.beliefs', 'governance.backlog', 'governance.graph', 'graph://nodes', 'graph://edges'],
    ioTools: [],
    sectionCount: 0,
  };
  return cachedGrammar;
}

function getSuggestions(input: string, grammar: GrammarMetadata): string[] {
  const trimmed = input.trim();
  if (!trimmed) {
    return ['SELECT nodes WHERE', 'CREATE PANEL "id" KIND grid SOURCE', 'CREATE VIZ "id" KIND force-graph SOURCE', 'SHOW beliefs', 'RESET'];
  }

  const upper = trimmed.toUpperCase();
  const lastSpace = trimmed.lastIndexOf(' ');
  const lastWord = lastSpace >= 0 ? trimmed.substring(lastSpace + 1).toUpperCase() : upper;

  // After SOURCE/FROM, suggest data sources
  if (upper.endsWith('SOURCE ') || upper.endsWith('FROM ')) {
    return grammar.dataSources;
  }

  // After KIND, suggest panel/viz kinds
  if (upper.endsWith('KIND ')) {
    return ['grid', 'force-graph', 'bar', 'sparkline', 'timeline'];
  }

  // After PIPE, suggest step keywords
  if (upper.endsWith('PIPE ')) {
    return ['FILTER', 'SORT', 'LIMIT', 'SKIP', 'DISTINCT', 'FLATTEN', 'GROUP BY'];
  }

  // After SHOW, suggest epistemic targets
  if (upper.endsWith('SHOW ')) {
    return ['beliefs', 'strategies', 'tensor', 'learners', 'journal', 'incompetence'];
  }

  // After SORT, suggest directions
  if (upper.endsWith(' ASC') || upper.endsWith(' DESC')) return [];
  const sortIdx = upper.lastIndexOf('SORT ');
  if (sortIdx >= 0 && upper.indexOf(' ', sortIdx + 5) > 0) {
    return ['ASC', 'DESC'];
  }

  // Partial keyword match
  if (lastWord.length >= 2) {
    return grammar.uiKeywords
      .filter(kw => kw.toUpperCase().indexOf(lastWord) === 0 && kw.toUpperCase() !== lastWord)
      .slice(0, 8);
  }

  return [];
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

interface IxqlCommandInputProps {
  onCommand: (result: IxqlParseResult) => void;
}

export const IxqlCommandInput: React.FC<IxqlCommandInputProps> = ({ onCommand }) => {
  const [visible, setVisible] = useState(false);
  const [value, setValue] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [suggestions, setSuggestions] = useState<string[]>([]);
  const [selectedSuggestion, setSelectedSuggestion] = useState(-1);
  const [grammar, setGrammar] = useState<GrammarMetadata | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  // Load grammar metadata on first open
  useEffect(() => {
    if (visible && !grammar) {
      fetchGrammarMetadata().then(setGrammar);
    }
  }, [visible, grammar]);

  // Update suggestions when input changes
  useEffect(() => {
    if (grammar && visible) {
      setSuggestions(getSuggestions(value, grammar));
      setSelectedSuggestion(-1);
    }
  }, [value, grammar, visible]);

  const toggle = useCallback(() => {
    setVisible(v => {
      if (!v) {
        setTimeout(() => inputRef.current?.focus(), 50);
      }
      return !v;
    });
    setError(null);
    setSuggestions([]);
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
      if (result.command.type === 'reset') {
        setValue('');
      }
    } else {
      setError(result.error);
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
          onKeyDown={e => {
            if (suggestions.length > 0) {
              if (e.key === 'ArrowDown') {
                e.preventDefault();
                setSelectedSuggestion(s => Math.min(s + 1, suggestions.length - 1));
              } else if (e.key === 'ArrowUp') {
                e.preventDefault();
                setSelectedSuggestion(s => Math.max(s - 1, -1));
              } else if (e.key === 'Tab' && selectedSuggestion >= 0) {
                e.preventDefault();
                const suggestion = suggestions[selectedSuggestion];
                const lastSpace = value.lastIndexOf(' ');
                const prefix = lastSpace >= 0 ? value.substring(0, lastSpace + 1) : '';
                setValue(prefix + suggestion + ' ');
                setSuggestions([]);
              }
            }
          }}
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
      {suggestions.length > 0 && !error && (
        <div style={{
          position: 'absolute', bottom: '100%', left: 32, right: 60,
          background: '#161b22', border: '1px solid #30363d', borderRadius: 4,
          maxHeight: 160, overflowY: 'auto', marginBottom: 2,
          boxShadow: '0 -4px 12px rgba(0,0,0,0.4)',
        }}>
          {suggestions.map((s, i) => (
            <div
              key={s}
              onClick={() => {
                const lastSpace = value.lastIndexOf(' ');
                const prefix = lastSpace >= 0 ? value.substring(0, lastSpace + 1) : '';
                setValue(prefix + s + ' ');
                setSuggestions([]);
                inputRef.current?.focus();
              }}
              style={{
                padding: '4px 10px',
                fontSize: 11,
                fontFamily: "'JetBrains Mono', monospace",
                color: i === selectedSuggestion ? '#ffd700' : '#8b949e',
                background: i === selectedSuggestion ? 'rgba(255,215,0,0.08)' : 'transparent',
                cursor: 'pointer',
              }}
            >
              {s}
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
