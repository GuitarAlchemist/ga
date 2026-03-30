// src/components/PrimeRadiant/TheoryTribunal.tsx
// Multi-model music theory panel — fans out a question to multiple LLM providers,
// displays side-by-side responses, and computes tetravalent consensus.

import React, { useState, useCallback, useRef } from 'react';
import { HEALTH_COLORS } from './types';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

type ProviderStatus = 'idle' | 'loading' | 'success' | 'error' | 'timeout';
type TetravalentValue = 'T' | 'F' | 'U' | 'C';
type ConsensusLevel = 'Unanimous' | 'Majority' | 'Split' | 'Contradictory';

interface ProviderResult {
  provider: string;
  icon: string;
  status: ProviderStatus;
  content: string;
  latencyMs: number;
  error?: string;
}

interface ConsensusAssessment {
  level: ConsensusLevel;
  tetravalent: TetravalentValue;
  description: string;
}

// ---------------------------------------------------------------------------
// Tetravalent color mapping (reuses HEALTH_COLORS palette)
// ---------------------------------------------------------------------------

const TETRAVALENT_COLORS: Record<TetravalentValue, string> = {
  T: HEALTH_COLORS.healthy,   // green — unanimous agreement
  F: '#CE93D8',               // purple — all reject the premise
  U: HEALTH_COLORS.watch,     // amber — insufficient data
  C: HEALTH_COLORS.freeze,    // red — contradictory
};

const TETRAVALENT_LABELS: Record<TetravalentValue, string> = {
  T: 'True — unanimous agreement',
  F: 'False — premise rejected',
  U: 'Unknown — insufficient responses',
  C: 'Contradictory — models disagree',
};

// ---------------------------------------------------------------------------
// Provider definitions
// ---------------------------------------------------------------------------

interface ProviderDef {
  id: string;
  label: string;
  icon: string;
  query: (prompt: string, signal: AbortSignal) => Promise<string>;
}

async function queryOllama(prompt: string, signal: AbortSignal): Promise<string> {
  const res = await fetch('/proxy/ollama/api/chat', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      model: 'llama3.2',
      messages: [{ role: 'user', content: prompt }],
      stream: false,
    }),
    signal,
  });
  if (!res.ok) throw new Error(`Ollama ${res.status}: ${res.statusText}`);
  const data: unknown = await res.json();
  const msg = data as { message?: { content?: string } };
  return msg?.message?.content ?? '(empty response)';
}

async function queryMistralAgent(prompt: string, signal: AbortSignal): Promise<string> {
  const apiKey = (import.meta as Record<string, Record<string, string>>).env?.VITE_MISTRAL_API_KEY ?? '';
  if (!apiKey) throw new Error('VITE_MISTRAL_API_KEY not configured');
  const res = await fetch('https://api.mistral.ai/v1/agents/completions', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${apiKey}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      agent_id: 'ag_019d3c30528e716fa8a5efeb9c8ae49c',
      messages: [{ role: 'user', content: prompt }],
    }),
    signal,
  });
  if (!res.ok) throw new Error(`Mistral ${res.status}: ${res.statusText}`);
  const data: unknown = await res.json();
  const choices = data as { choices?: Array<{ message?: { content?: string } }> };
  return choices?.choices?.[0]?.message?.content ?? '(empty response)';
}

const PROVIDERS: ProviderDef[] = [
  { id: 'ollama', label: 'Ollama (Llama 3.2)', icon: '\u{1F999}', query: queryOllama },
  { id: 'mistral', label: 'Mistral Agent', icon: '\u{1F32C}\u{FE0F}', query: queryMistralAgent },
];

// ---------------------------------------------------------------------------
// Consensus computation
// ---------------------------------------------------------------------------

function computeConsensus(results: ProviderResult[]): ConsensusAssessment {
  const succeeded = results.filter((r) => r.status === 'success');
  const failed = results.filter((r) => r.status === 'error' || r.status === 'timeout');

  if (succeeded.length === 0) {
    return {
      level: 'Split',
      tetravalent: 'U',
      description: `No successful responses (${failed.length} failed)`,
    };
  }

  if (succeeded.length === 1) {
    return {
      level: 'Split',
      tetravalent: 'U',
      description: 'Only one provider responded — cannot assess consensus',
    };
  }

  // Simple heuristic: compare response lengths and keyword overlap
  const contents = succeeded.map((r) => r.content.toLowerCase());
  const allKeywords = contents.map((c) =>
    new Set(c.split(/\s+/).filter((w) => w.length > 4)),
  );

  // Jaccard similarity between all pairs
  let totalSim = 0;
  let pairs = 0;
  for (let i = 0; i < allKeywords.length; i++) {
    for (let j = i + 1; j < allKeywords.length; j++) {
      const a = allKeywords[i];
      const b = allKeywords[j];
      let intersection = 0;
      for (const word of a) {
        if (b.has(word)) intersection++;
      }
      const union = a.size + b.size - intersection;
      totalSim += union > 0 ? intersection / union : 0;
      pairs++;
    }
  }

  const avgSim = pairs > 0 ? totalSim / pairs : 0;

  if (avgSim > 0.3) {
    return {
      level: 'Unanimous',
      tetravalent: 'T',
      description: `High agreement across ${succeeded.length} providers (similarity: ${(avgSim * 100).toFixed(0)}%)`,
    };
  }
  if (avgSim > 0.15) {
    return {
      level: 'Majority',
      tetravalent: 'T',
      description: `Moderate agreement (similarity: ${(avgSim * 100).toFixed(0)}%)`,
    };
  }
  if (avgSim > 0.05) {
    return {
      level: 'Split',
      tetravalent: 'C',
      description: `Low agreement — models diverge (similarity: ${(avgSim * 100).toFixed(0)}%)`,
    };
  }

  return {
    level: 'Contradictory',
    tetravalent: 'C',
    description: `Models strongly disagree (similarity: ${(avgSim * 100).toFixed(0)}%)`,
  };
}

// ---------------------------------------------------------------------------
// Status badge component
// ---------------------------------------------------------------------------

function StatusBadge({ status }: { status: ProviderStatus }) {
  const map: Record<ProviderStatus, { label: string; color: string }> = {
    idle: { label: 'Idle', color: '#8b949e' },
    loading: { label: 'Querying...', color: '#58a6ff' },
    success: { label: 'OK', color: '#33CC66' },
    error: { label: 'Error', color: '#FF4444' },
    timeout: { label: 'Timeout', color: '#FFB300' },
  };
  const info = map[status];
  return (
    <span className="tribunal-panel__status-badge" style={{ color: info.color, borderColor: info.color }}>
      {info.label}
    </span>
  );
}

// ---------------------------------------------------------------------------
// Main component
// ---------------------------------------------------------------------------

const TIMEOUT_MS = 30_000;
const EXAMPLE_PROMPTS = [
  'Analyze Cmaj7 - Dm7 - G7 - Cmaj7',
  'What makes the tritone substitution work?',
  'Compare Dorian and Aeolian modes',
  'Explain secondary dominants in jazz harmony',
];

export function TheoryTribunal(): React.ReactElement {
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<ProviderResult[]>([]);
  const [isRunning, setIsRunning] = useState(false);
  const [consensus, setConsensus] = useState<ConsensusAssessment | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  const runQuery = useCallback(async (prompt: string) => {
    if (!prompt.trim()) return;

    // Abort any in-flight request
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    setIsRunning(true);
    setConsensus(null);

    // Initialize results with loading state
    const initial: ProviderResult[] = PROVIDERS.map((p) => ({
      provider: p.label,
      icon: p.icon,
      status: 'loading' as ProviderStatus,
      content: '',
      latencyMs: 0,
    }));
    setResults([...initial]);

    // Fan out to all providers in parallel
    const settled = await Promise.allSettled(
      PROVIDERS.map(async (prov, idx) => {
        const start = performance.now();
        try {
          const timeoutId = setTimeout(() => controller.abort(), TIMEOUT_MS);
          const content = await prov.query(prompt, controller.signal);
          clearTimeout(timeoutId);
          const latencyMs = Math.round(performance.now() - start);
          const result: ProviderResult = {
            provider: prov.label,
            icon: prov.icon,
            status: 'success',
            content,
            latencyMs,
          };
          setResults((prev) => {
            const next = [...prev];
            next[idx] = result;
            return next;
          });
          return result;
        } catch (err: unknown) {
          const latencyMs = Math.round(performance.now() - start);
          const isAbort = err instanceof DOMException && err.name === 'AbortError';
          const result: ProviderResult = {
            provider: prov.label,
            icon: prov.icon,
            status: isAbort ? 'timeout' : 'error',
            content: '',
            latencyMs,
            error: err instanceof Error ? err.message : 'Unknown error',
          };
          setResults((prev) => {
            const next = [...prev];
            next[idx] = result;
            return next;
          });
          return result;
        }
      }),
    );

    // Extract fulfilled results for consensus
    const finalResults = settled.map((s) =>
      s.status === 'fulfilled' ? s.value : null,
    ).filter((r): r is ProviderResult => r !== null);

    setConsensus(computeConsensus(finalResults));
    setIsRunning(false);
  }, []);

  const handleSubmit = useCallback((e: React.FormEvent) => {
    e.preventDefault();
    void runQuery(query);
  }, [query, runQuery]);

  const handleReask = useCallback(() => {
    void runQuery(query);
  }, [query, runQuery]);

  return (
    <div className="tribunal-panel">
      {/* Header */}
      <div className="tribunal-panel__header">
        <span className="tribunal-panel__title">Theory Tribunal</span>
        <span className="tribunal-panel__subtitle">Multi-model music theory analysis</span>
      </div>

      {/* Input */}
      <form className="tribunal-panel__form" onSubmit={handleSubmit}>
        <input
          className="tribunal-panel__input"
          type="text"
          placeholder="Ask a music theory question..."
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          disabled={isRunning}
        />
        <button
          className="tribunal-panel__submit"
          type="submit"
          disabled={isRunning || !query.trim()}
        >
          {isRunning ? 'Asking...' : 'Ask'}
        </button>
      </form>

      {/* Example prompts */}
      {results.length === 0 && (
        <div className="tribunal-panel__examples">
          <span className="tribunal-panel__examples-label">Try:</span>
          {EXAMPLE_PROMPTS.map((p) => (
            <button
              key={p}
              className="tribunal-panel__example-btn"
              onClick={() => { setQuery(p); void runQuery(p); }}
            >
              {p}
            </button>
          ))}
        </div>
      )}

      {/* Consensus section */}
      {consensus && (
        <div
          className="tribunal-panel__consensus"
          style={{ borderColor: TETRAVALENT_COLORS[consensus.tetravalent] }}
        >
          <div className="tribunal-panel__consensus-header">
            <span
              className="tribunal-panel__consensus-dot"
              style={{ backgroundColor: TETRAVALENT_COLORS[consensus.tetravalent] }}
            />
            <span className="tribunal-panel__consensus-level">{consensus.level}</span>
            <span
              className="tribunal-panel__consensus-tv"
              style={{ color: TETRAVALENT_COLORS[consensus.tetravalent] }}
            >
              [{consensus.tetravalent}]
            </span>
          </div>
          <div className="tribunal-panel__consensus-desc">{consensus.description}</div>
          <div className="tribunal-panel__consensus-label">
            {TETRAVALENT_LABELS[consensus.tetravalent]}
          </div>
        </div>
      )}

      {/* Provider cards */}
      <div className="tribunal-panel__cards">
        {results.map((r) => (
          <div key={r.provider} className="tribunal-panel__card">
            <div className="tribunal-panel__card-header">
              <span className="tribunal-panel__card-icon">{r.icon}</span>
              <span className="tribunal-panel__card-provider">{r.provider}</span>
              <StatusBadge status={r.status} />
              {r.latencyMs > 0 && (
                <span className="tribunal-panel__latency">{r.latencyMs}ms</span>
              )}
            </div>
            <div className="tribunal-panel__card-body">
              {r.status === 'loading' && (
                <div className="tribunal-panel__loading">
                  <span className="tribunal-panel__spinner" />
                  Querying provider...
                </div>
              )}
              {r.status === 'success' && (
                <pre className="tribunal-panel__response">{r.content}</pre>
              )}
              {(r.status === 'error' || r.status === 'timeout') && (
                <div className="tribunal-panel__error">
                  {r.error ?? 'Request failed'}
                </div>
              )}
            </div>
          </div>
        ))}
      </div>

      {/* Re-ask button */}
      {results.length > 0 && !isRunning && (
        <div className="tribunal-panel__footer">
          <button className="tribunal-panel__reask" onClick={handleReask}>
            Re-ask
          </button>
        </div>
      )}
    </div>
  );
}
