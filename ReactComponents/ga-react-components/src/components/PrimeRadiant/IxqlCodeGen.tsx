// src/components/PrimeRadiant/IxqlCodeGen.tsx
// AI-powered IXQL command generator panel — uses Codestral to translate
// natural language into IXQL commands for the Prime Radiant governance engine.

import React, { useCallback, useRef, useState } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface IxqlCodeGenProps {
  onRunCommand?: (ixql: string) => void;
}

interface ChatChoice {
  message: { content: string };
}

interface ChatResponse {
  choices: ChatChoice[];
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const CODESTRAL_ENDPOINT = '/proxy/codestral/v1/chat/completions';
const MODEL = 'codestral-latest';

const SYSTEM_PROMPT = `You are an IXQL command generator for Prime Radiant governance visualization.

Commands:
- SELECT * WHERE health.staleness > 0.5 SET glow = true, color = "#FF4444"
- RESET
- CREATE PANEL "my-panel" KIND grid FROM "/api/data" COLUMNS name, status
- BIND HEALTH "panel-id" TO "/api/status" WHEN status = "error" THEN error
- SHOW tower | HIDE tower
- DIAGNOSE | HEALTH CHECK
- FIX errors | FIX signals | FIX all

Return ONLY the IXQL command, no explanation.`;

interface QuickExample {
  label: string;
  prompt: string;
}

const EXAMPLES: QuickExample[] = [
  { label: 'Show all warning nodes', prompt: 'Show all warning nodes' },
  { label: 'Create a panel for beliefs', prompt: 'Create a panel for beliefs' },
  { label: 'Highlight stale policies', prompt: 'Highlight stale policies' },
  { label: 'Health check', prompt: 'Health check' },
  { label: 'Fix all signals', prompt: 'Fix all signals' },
];

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export const IxqlCodeGen: React.FC<IxqlCodeGenProps> = ({ onRunCommand }) => {
  const [input, setInput] = useState('');
  const [output, setOutput] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const generate = useCallback(async (prompt: string) => {
    if (!prompt.trim()) return;
    setLoading(true);
    setError('');
    setOutput('');

    try {
      const res = await fetch(CODESTRAL_ENDPOINT, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          model: MODEL,
          messages: [
            { role: 'system', content: SYSTEM_PROMPT },
            { role: 'user', content: prompt.trim() },
          ],
          temperature: 0.1,
          max_tokens: 256,
        }),
      });

      if (!res.ok) {
        const text = await res.text();
        throw new Error(`API ${res.status}: ${text.slice(0, 200)}`);
      }

      const data: ChatResponse = await res.json();
      const generated = data.choices[0]?.message?.content?.trim() ?? '';
      setOutput(generated);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Generation failed';
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, []);

  const handleGenerate = useCallback(() => {
    void generate(input);
  }, [generate, input]);

  const handleQuickExample = useCallback((prompt: string) => {
    setInput(prompt);
    void generate(prompt);
  }, [generate]);

  const handleRun = useCallback(() => {
    if (output && onRunCommand) {
      onRunCommand(output);
    }
  }, [output, onRunCommand]);

  const handleKeyDown = useCallback((e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
      e.preventDefault();
      void generate(input);
    }
  }, [generate, input]);

  return (
    <div className="ixql-gen">
      <div className="ixql-gen__header">
        <div className="ixql-gen__title">IXQL Generator</div>
        <div className="ixql-gen__subtitle">Codestral-powered command generation</div>
      </div>

      {/* Quick examples */}
      <div className="ixql-gen__examples">
        {EXAMPLES.map((ex) => (
          <button
            key={ex.label}
            className="ixql-gen__example-btn"
            onClick={() => handleQuickExample(ex.prompt)}
            disabled={loading}
          >
            {ex.label}
          </button>
        ))}
      </div>

      {/* Input */}
      <div className="ixql-gen__input-section">
        <textarea
          ref={textareaRef}
          className="ixql-gen__textarea"
          placeholder="Describe what you want..."
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={handleKeyDown}
          rows={3}
          disabled={loading}
        />
        <div className="ixql-gen__controls">
          <span className="ixql-gen__hint">Ctrl+Enter to generate</span>
          <button
            className="ixql-gen__generate-btn"
            onClick={handleGenerate}
            disabled={loading || !input.trim()}
          >
            {loading ? 'Generating...' : 'Generate'}
          </button>
        </div>
      </div>

      {/* Error */}
      {error && (
        <div className="ixql-gen__error">{error}</div>
      )}

      {/* Output */}
      {output && (
        <div className="ixql-gen__output-section">
          <div className="ixql-gen__output-label">Generated IXQL</div>
          <pre className="ixql-gen__output">{output}</pre>
          <div className="ixql-gen__output-controls">
            <button
              className="ixql-gen__copy-btn"
              onClick={() => { void navigator.clipboard.writeText(output); }}
            >
              Copy
            </button>
            {onRunCommand && (
              <button
                className="ixql-gen__run-btn"
                onClick={handleRun}
              >
                Run
              </button>
            )}
          </div>
        </div>
      )}
    </div>
  );
};
