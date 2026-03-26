// src/components/PrimeRadiant/LLMStatus.tsx
// Compact LLM provider status — models, tokens, credits, active plan

import React, { useEffect, useState } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
interface LLMProvider {
  name: string;
  icon: string;
  model: string;
  plan: string;
  tokensUsed?: string;
  tokensLimit?: string;
  creditsLeft?: string;
  status: 'active' | 'limited' | 'depleted';
}

// ---------------------------------------------------------------------------
// Dynamic LLM status — fetches from backend, falls back to detection
// ---------------------------------------------------------------------------
async function fetchLLMProviders(): Promise<LLMProvider[]> {
  // Try backend endpoint first
  try {
    const res = await fetch('/api/llm/status');
    if (res.ok) return await res.json();
  } catch { /* fall through */ }

  // Detect from environment / available config
  const providers: LLMProvider[] = [];

  // Anthropic — always available (we run on Claude)
  providers.push({
    name: 'Anthropic',
    icon: 'A',
    model: 'Claude Opus 4.6',
    plan: 'Max (1M ctx)',
    status: 'active',
  });

  // Check if Claude proxy is configured (indicates active usage)
  const proxyUrl = typeof import.meta !== 'undefined'
    ? (import.meta as { env?: Record<string, string> }).env?.VITE_CLAUDE_PROXY_URL
    : undefined;
  if (proxyUrl) {
    try {
      const res = await fetch(proxyUrl, { method: 'OPTIONS' });
      if (res.ok) providers[0].status = 'active';
    } catch {
      providers[0].status = 'limited';
    }
  }

  // Check Ollama local
  try {
    const res = await fetch('http://localhost:11434/api/tags', { signal: AbortSignal.timeout(2000) });
    if (res.ok) {
      const data = await res.json();
      const models = data.models ?? [];
      if (models.length > 0) {
        providers.push({
          name: 'Ollama',
          icon: 'L',
          model: models[0].name ?? 'local',
          plan: `${models.length} model${models.length > 1 ? 's' : ''}`,
          status: 'active',
        });
      }
    }
  } catch { /* Ollama not running */ }

  return providers;
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
const STATUS_DOT: Record<LLMProvider['status'], string> = {
  active: '#33CC66',
  limited: '#FFB300',
  depleted: '#FF4444',
};

export const LLMStatus: React.FC = () => {
  const [providers, setProviders] = useState<LLMProvider[]>([]);
  const [collapsed, setCollapsed] = useState(false);

  useEffect(() => {
    fetchLLMProviders().then(setProviders);
    // Refresh every 60 seconds
    const interval = setInterval(() => {
      fetchLLMProviders().then(setProviders);
    }, 60000);
    return () => clearInterval(interval);
  }, []);

  return (
    <div className="prime-radiant__llm-status">
      <div
        className="prime-radiant__llm-header"
        onClick={() => setCollapsed(!collapsed)}
      >
        <span className="prime-radiant__llm-title">
          LLM
          {providers.map((p) => (
            <span
              key={p.name}
              className="prime-radiant__llm-dot"
              style={{ backgroundColor: STATUS_DOT[p.status] }}
              title={`${p.name}: ${p.status}`}
            />
          ))}
        </span>
        <span className="prime-radiant__llm-toggle">{collapsed ? '▶' : '▼'}</span>
      </div>

      {!collapsed && (
        <div className="prime-radiant__llm-list">
          {providers.map((p) => (
            <div key={p.name} className="prime-radiant__llm-item">
              <span className="prime-radiant__llm-icon" style={{
                color: STATUS_DOT[p.status],
              }}>{p.icon}</span>
              <div className="prime-radiant__llm-info">
                <div className="prime-radiant__llm-model">{p.model}</div>
                <div className="prime-radiant__llm-meta">
                  {p.plan}
                  {p.tokensUsed && ` · ${p.tokensUsed}/${p.tokensLimit}`}
                  {p.creditsLeft && ` · ${p.creditsLeft}`}
                </div>
              </div>
              <span className="prime-radiant__llm-badge" style={{
                color: STATUS_DOT[p.status],
                borderColor: `${STATUS_DOT[p.status]}44`,
              }}>
                {p.status === 'active' ? '●' : p.status === 'limited' ? '◐' : '○'}
              </span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
