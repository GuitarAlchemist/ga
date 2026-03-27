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
  usage?: {
    today: number;       // tokens used today
    thisWeek: number;    // tokens used this week
    dailyLimit?: number; // daily token limit (if known)
    weeklyLimit?: number;
    percentUsed?: number; // 0-100
  };
}

// ---------------------------------------------------------------------------
// Claude usage tracking — reads from localStorage, updated by ChatWidget
// ---------------------------------------------------------------------------
function getClaudeUsage(): NonNullable<LLMProvider['usage']> {
  try {
    const raw = localStorage.getItem('ga-claude-usage');
    if (raw) {
      const data = JSON.parse(raw);
      const today = new Date().toISOString().slice(0, 10);
      const weekStart = new Date();
      weekStart.setDate(weekStart.getDate() - weekStart.getDay());
      const weekKey = weekStart.toISOString().slice(0, 10);

      const todayTokens = data.daily?.[today] ?? 0;
      const weekTokens = Object.entries(data.daily ?? {})
        .filter(([d]) => d >= weekKey)
        .reduce((sum, [, v]) => sum + (v as number), 0);

      // Max plan: ~45M tokens/month ≈ 1.5M/day, ~10M/week
      const dailyLimit = 1_500_000;
      const weeklyLimit = 10_000_000;

      return {
        today: todayTokens,
        thisWeek: weekTokens,
        dailyLimit,
        weeklyLimit,
        percentUsed: Math.round((todayTokens / dailyLimit) * 100),
      };
    }
  } catch { /* ignore */ }

  return { today: 0, thisWeek: 0, percentUsed: 0 };
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
  // Read usage from localStorage (tracked by ChatWidget proxy calls)
  const claudeUsage = getClaudeUsage();
  providers.push({
    name: 'Anthropic',
    icon: 'A',
    model: 'Claude Opus 4.6',
    plan: 'Max (1M ctx)',
    status: claudeUsage.percentUsed > 90 ? 'limited' : 'active',
    usage: claudeUsage,
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

  // OpenAI — available via MCP integration
  providers.push({
    name: 'OpenAI',
    icon: 'O',
    model: 'GPT-4o',
    plan: 'via MCP',
    status: 'active',
  });

  // Safety net: ensure at least Anthropic is always present
  if (providers.length === 0) {
    providers.push({
      name: 'Anthropic',
      icon: 'A',
      model: 'Claude Opus 4.6',
      plan: 'Max (1M ctx)',
      status: 'active',
    });
  }

  return providers;
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
function formatTokens(n: number): string {
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
  if (n >= 1_000) return `${(n / 1_000).toFixed(0)}K`;
  return String(n);
}

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
                  {p.usage && p.usage.today > 0 && ` · ${formatTokens(p.usage.today)} today`}
                  {p.creditsLeft && ` · ${p.creditsLeft}`}
                </div>
              </div>
              <span className="prime-radiant__llm-badge" style={{
                color: STATUS_DOT[p.status],
                borderColor: `${STATUS_DOT[p.status]}44`,
              }}>
                {p.status === 'active' ? '●' : p.status === 'limited' ? '◐' : '○'}
              </span>
              {p.usage && p.usage.dailyLimit && (
                <div className="prime-radiant__llm-usage">
                  <div className="prime-radiant__llm-usage-bar">
                    <div
                      className="prime-radiant__llm-usage-fill"
                      style={{
                        width: `${Math.min(p.usage.percentUsed ?? 0, 100)}%`,
                        backgroundColor: (p.usage.percentUsed ?? 0) > 80 ? '#FF4444' : (p.usage.percentUsed ?? 0) > 50 ? '#FFB300' : '#33CC66',
                      }}
                    />
                  </div>
                  <div className="prime-radiant__llm-usage-label">
                    {formatTokens(p.usage.today)}/{formatTokens(p.usage.dailyLimit)} daily
                    {p.usage.thisWeek > 0 && ` · ${formatTokens(p.usage.thisWeek)} this week`}
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
