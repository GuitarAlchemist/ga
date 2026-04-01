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

const SYSTEM_PROMPT = `You are an IXQL command generator for the Prime Radiant governance visualization engine.
This system governs the GuitarAlchemist ecosystem: ix (Rust ML), TARS (F# reasoning), Demerzel (governance), and GA (music).

IXQL commands:
- SELECT nodes WHERE type = "policy" SET glow = true pulse = 2
- SELECT edges SET color = "#ffd700"
- RESET
- CREATE PANEL "id" KIND grid SOURCE governance.beliefs PROJECT { id, proposition, truth_value, confidence } PIPE FILTER confidence > 0.7 SORT confidence DESC LIMIT 20 REFRESH 30s GOVERNED BY article=1,7
- CREATE VIZ "id" KIND force-graph SOURCE governance.graph
- CREATE VIZ "id" KIND truth-lattice SOURCE governance.beliefs
- CREATE VIZ "id" KIND bar SOURCE governance.backlog
- CREATE FORM "id" FIELDS [ truth_value: enum(T,P,U,D,F,C), confidence: slider(0,1) ] HEXAVALENT validation=true SUBMIT COMMAND governance.updateBelief ON_SUCCESS REFRESH "beliefs-panel" GOVERNED BY article=3
- SHOW beliefs | SHOW strategies | SHOW tensor | SHOW learners | SHOW journal | SHOW incompetence
- ON VIOLATION IN beliefs WHEN staleness > 0.8 SEVERITY critical THEN SELECT nodes WHERE type = "belief" SET pulse = 3 NOTIFY VIA algedonic
- SAVE QUERY "name" AS artifact RATIONALE "why"
- PIPE FILTER field op value | SORT field ASC/DESC | LIMIT n | SKIP n | DISTINCT field | GROUP BY field COUNT | TOP n BY field
- DIAGNOSE | HEALTH CHECK

Hexavalent logic: T=True, P=Probable, U=Unknown, D=Doubtful, F=False, C=Contradictory.
Return ONLY the IXQL command, no explanation.`;

interface QuickExample {
  label: string;
  prompt: string;
}

const EXAMPLES: QuickExample[] = [
  // Real ix/TARS/Demerzel governance use cases
  { label: 'Belief dashboard', prompt: 'Create a grid panel showing all governance beliefs with truth value, confidence, and staleness, sorted by confidence descending, refreshing every 30 seconds, governed by Article 1 Truthfulness' },
  { label: 'Truth lattice', prompt: 'Create a truth lattice visualization from governance beliefs to see how beliefs move through T/P/U/D/F/C states' },
  { label: 'Stale policy alert', prompt: 'Set up a violation monitor that fires when any belief staleness exceeds 0.8, severity critical, highlighting those nodes with red pulse and notifying the algedonic channel' },
  { label: 'Contradictions', prompt: 'Show all nodes where truth_value is Contradictory and make them pulse magenta with high opacity' },
  { label: 'ix ML pipeline status', prompt: 'Create a grid panel showing the backlog items filtered to type=feature, sorted by severity descending, top 10, governed by Article 8 Observability' },
  { label: 'Belief editor form', prompt: 'Create a hexavalent form for editing beliefs with truth_value enum (T,P,U,D,F,C), confidence slider 0 to 1, and justification text field, submitting to governance.updateBelief, refreshing the beliefs panel on success' },
  { label: 'Constitutional compliance', prompt: 'Show all policies governed by Article 9 Bounded Autonomy and highlight any with confidence below 0.5 in orange' },
  { label: 'TARS reasoning health', prompt: 'Create a bar chart visualization of governance predictions grouped by status, to see the health of TARS reasoning outputs' },
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

  const [runStatus, setRunStatus] = useState<string | null>(null);

  const handleRun = useCallback(() => {
    if (!output) {
      setRunStatus('No command generated — click an example or type a prompt first');
      return;
    }
    if (!onRunCommand) {
      setRunStatus('Command dispatch not connected');
      return;
    }
    try {
      onRunCommand(output);
      setRunStatus('Executed: ' + output.substring(0, 60) + (output.length > 60 ? '...' : ''));
    } catch (err) {
      setRunStatus('Error: ' + String(err));
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
      {runStatus && (
        <div style={{
          padding: '6px 12px',
          fontSize: 10,
          color: runStatus.startsWith('Error') ? '#ef4444' : runStatus.startsWith('No ') ? '#f97316' : '#22c55e',
          borderTop: '1px solid rgba(255,255,255,0.06)',
          fontFamily: "'JetBrains Mono', monospace",
        }}>
          {runStatus}
        </div>
      )}
    </div>
  );
};
