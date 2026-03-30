// src/components/PrimeRadiant/LLMStatus.tsx
// Compact LLM provider status panel — real health checks via Vite proxy,
// env-var detection for API-key providers, localStorage usage tracking.
// Addresses GitHub issue #33.

import React, { useEffect, useState, useCallback } from 'react';
import type { PanelStatus } from './IconRail';
import { useSessionUsage, getQuotaPercent } from './LLMUsageTracker';
import type { ModelUsage } from './LLMUsageTracker';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

type ProviderStatus = 'active' | 'configured' | 'not-configured' | 'offline';

type ProviderCategory = 'cloud' | 'local' | 'tools';

const CATEGORY_LABELS: Record<ProviderCategory, string> = {
  cloud: 'Cloud AI',
  local: 'Local',
  tools: 'Tools',
};

const CATEGORY_ORDER: ProviderCategory[] = ['cloud', 'local', 'tools'];

interface LLMProvider {
  name: string;
  icon: string;
  detail: string;          // model name or summary (e.g. "3 models loaded")
  status: ProviderStatus;
  statusHint?: string;     // tooltip text
  category: ProviderCategory;
  usage?: {
    today: number;
    thisWeek: number;
    dailyLimit?: number;
    weeklyLimit?: number;
    percentUsed?: number;  // 0-100
  };
}

// ---------------------------------------------------------------------------
// Backend API response shape (when /api/llm/status exists)
// ---------------------------------------------------------------------------

interface BackendProviderStatus {
  name: string;
  status: string;
  detail?: string;
  models?: string[];
  modelCount?: number;
}

// ---------------------------------------------------------------------------
// Claude usage tracking — reads from localStorage, updated by ChatWidget
// ---------------------------------------------------------------------------

function getClaudeUsage(): NonNullable<LLMProvider['usage']> {
  try {
    const raw = localStorage.getItem('ga-claude-usage');
    if (raw) {
      const data: Record<string, Record<string, number>> = JSON.parse(raw);
      const today = new Date().toISOString().slice(0, 10);
      const weekStart = new Date();
      weekStart.setDate(weekStart.getDate() - weekStart.getDay());
      const weekKey = weekStart.toISOString().slice(0, 10);

      const todayTokens = data.daily?.[today] ?? 0;
      const weekTokens = Object.entries(data.daily ?? {})
        .filter(([d]) => d >= weekKey)
        .reduce((sum, [, v]) => sum + (v as number), 0);

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
  } catch { /* ignore corrupt localStorage */ }

  return { today: 0, thisWeek: 0, percentUsed: 0 };
}

// ---------------------------------------------------------------------------
// Env-var helpers
// ---------------------------------------------------------------------------

function envVar(key: string): string | undefined {
  try {
    return (import.meta as { env?: Record<string, string> }).env?.[key];
  } catch {
    return undefined;
  }
}

function isEnvTrue(key: string): boolean {
  const v = envVar(key);
  return v !== undefined && v !== '' && v !== '0' && v !== 'false';
}

// ---------------------------------------------------------------------------
// Individual provider checks
// ---------------------------------------------------------------------------

async function checkOllama(): Promise<LLMProvider> {
  const base: LLMProvider = {
    name: 'Ollama',
    icon: '\u{1F999}',   // llama emoji
    detail: 'local inference',
    status: 'offline',
    statusHint: 'localhost:11434 not reachable',
    category: 'local',
  };

  try {
    // Goes through Vite proxy at /proxy/ollama to avoid CORS
    const res = await fetch('/proxy/ollama/api/tags', {
      signal: AbortSignal.timeout(3000),
    });
    if (res.ok) {
      const data: { models?: { name: string }[] } = await res.json();
      const models = data.models ?? [];
      if (models.length > 0) {
        const names = models.map(m => m.name);
        base.status = 'active';
        base.detail = `${models.length} model${models.length > 1 ? 's' : ''}: ${names.slice(0, 3).join(', ')}${models.length > 3 ? '...' : ''}`;
        base.statusHint = names.join(', ');
      } else {
        base.status = 'active';
        base.detail = 'running (no models pulled)';
        base.statusHint = 'Ollama is running but no models are loaded';
      }
    }
  } catch {
    // Ollama not running — stays offline
  }

  return base;
}

async function checkDockerModelRunner(): Promise<LLMProvider> {
  const base: LLMProvider = {
    name: 'Docker Models',
    icon: '\u{1F433}',   // whale emoji
    detail: 'Docker Model Runner',
    status: 'offline',
    statusHint: 'localhost:12434 not reachable',
    category: 'local',
  };

  try {
    const res = await fetch('/proxy/docker-models/engines/llama.cpp/v1/models', {
      signal: AbortSignal.timeout(3000),
    });
    if (res.ok) {
      const data: { data?: { id: string }[] } = await res.json();
      const models = data.data ?? [];
      base.status = 'active';
      base.detail = models.length > 0
        ? `${models.length} model${models.length > 1 ? 's' : ''}`
        : 'running';
      base.statusHint = models.length > 0
        ? models.map(m => m.id).join(', ')
        : 'Docker Model Runner is active';
    }
  } catch {
    // not running
  }

  return base;
}

function checkAnthropic(): LLMProvider {
  const usage = getClaudeUsage();
  const proxyUrl = envVar('VITE_CLAUDE_PROXY_URL');

  // Anthropic is always at least "configured" since this app runs on Claude
  const status: ProviderStatus = proxyUrl
    ? 'active'
    : 'configured';

  return {
    name: 'Anthropic',
    icon: 'A',
    detail: 'Claude Opus 4.6',
    status: usage.percentUsed !== undefined && usage.percentUsed > 90 ? 'configured' : status,
    statusHint: proxyUrl ? `Proxy: ${proxyUrl}` : 'Claude Code session active',
    category: 'cloud',
    usage,
  };
}

function checkOpenAI(): LLMProvider {
  const configured = isEnvTrue('VITE_OPENAI_CONFIGURED');
  return {
    name: 'OpenAI',
    icon: 'O',
    detail: 'GPT-4o',
    status: configured ? 'configured' : 'not-configured',
    statusHint: configured ? 'API key configured' : 'Set VITE_OPENAI_CONFIGURED=1',
    category: 'cloud',
  };
}

function checkGemini(): LLMProvider {
  const configured = isEnvTrue('VITE_GEMINI_CONFIGURED');
  return {
    name: 'Gemini',
    icon: 'G',
    detail: 'Gemini 2.5 Pro',
    status: configured ? 'configured' : 'not-configured',
    statusHint: configured ? 'API key configured' : 'Set VITE_GEMINI_CONFIGURED=1',
    category: 'cloud',
  };
}

function checkVoxtral(): LLMProvider {
  const configured = isEnvTrue('VITE_VOXTRAL_CONFIGURED');
  return {
    name: 'Voxtral TTS',
    icon: 'V',
    detail: 'Mistral voxtral-mini-tts',
    status: configured ? 'configured' : 'not-configured',
    statusHint: configured ? 'API key configured' : 'Set VITE_VOXTRAL_CONFIGURED=1',
    category: 'tools',
  };
}

function checkCodex(): LLMProvider {
  // Codex uses OPENAI_API_KEY — if OpenAI is configured, Codex inherits
  const configured = isEnvTrue('VITE_CODEX_CONFIGURED') || isEnvTrue('VITE_OPENAI_CONFIGURED');
  return {
    name: 'Codex',
    icon: 'X',
    detail: 'OpenAI Codex CLI',
    status: configured ? 'configured' : 'not-configured',
    statusHint: configured ? 'codex CLI available' : 'Set VITE_CODEX_CONFIGURED=1',
    category: 'tools',
  };
}

function checkCodestral(): LLMProvider {
  const configured = isEnvTrue('VITE_CODESTRAL_CONFIGURED');
  return {
    name: 'Codestral',
    icon: 'M',
    detail: 'Mistral Codestral',
    status: configured ? 'configured' : 'not-configured',
    statusHint: configured ? 'Codestral API key configured' : 'Set VITE_CODESTRAL_CONFIGURED=1',
    category: 'tools',
  };
}

// ---------------------------------------------------------------------------
// Aggregated fetch — tries backend first, then client-side checks
// ---------------------------------------------------------------------------

async function fetchLLMProviders(): Promise<LLMProvider[]> {
  // Try the backend endpoint first (server-side checks avoid CORS entirely)
  try {
    const res = await fetch('/api/llm/status', { signal: AbortSignal.timeout(3000) });
    if (res.ok) {
      const data: BackendProviderStatus[] = await res.json();
      return data.map(p => ({
        name: p.name,
        icon: providerIcon(p.name),
        detail: p.detail ?? (p.models ? p.models.join(', ') : `${p.modelCount ?? 0} models`),
        status: mapBackendStatus(p.status),
        statusHint: p.models?.join(', '),
        category: providerCategory(p.name),
      }));
    }
  } catch { /* backend not available — do client-side checks */ }

  // Client-side parallel checks
  const [ollama, docker] = await Promise.all([
    checkOllama(),
    checkDockerModelRunner(),
  ]);

  // Synchronous checks for env-var providers
  const anthropic = checkAnthropic();
  const openai = checkOpenAI();
  const gemini = checkGemini();
  const voxtral = checkVoxtral();
  const codex = checkCodex();
  const codestral = checkCodestral();

  return [anthropic, ollama, openai, gemini, voxtral, docker, codex, codestral];
}

function providerIcon(name: string): string {
  const map: Record<string, string> = {
    anthropic: 'A', ollama: '\u{1F999}', openai: 'O', gemini: 'G',
    voxtral: 'V', 'docker models': '\u{1F433}', codex: 'X', codestral: 'M',
  };
  return map[name.toLowerCase()] ?? name.charAt(0);
}

function providerCategory(name: string): ProviderCategory {
  const n = name.toLowerCase();
  if (['ollama', 'docker models', 'docker'].includes(n)) return 'local';
  if (['voxtral', 'codex', 'voxtral tts', 'codestral'].includes(n)) return 'tools';
  return 'cloud';
}

function mapBackendStatus(s: string): ProviderStatus {
  if (s === 'active' || s === 'online') return 'active';
  if (s === 'configured') return 'configured';
  if (s === 'offline' || s === 'error') return 'offline';
  return 'not-configured';
}

// ---------------------------------------------------------------------------
// Hook — exposes LLM health for icon rail status dot
// ---------------------------------------------------------------------------

export function useLLMHealth(): PanelStatus {
  const [status, setStatus] = useState<PanelStatus>(null);

  const refresh = useCallback(async () => {
    const providers = await fetchLLMProviders();
    if (providers.length === 0) { setStatus(null); return; }
    const activeCount = providers.filter(p => p.status === 'active').length;
    const offlineCount = providers.filter(p => p.status === 'offline').length;
    if (offlineCount > 0 && activeCount === 0) setStatus('error');
    else if (offlineCount > 0) setStatus('warn');
    else if (activeCount > 0) setStatus('ok');
    else setStatus(null);
  }, []);

  useEffect(() => {
    refresh();
    const interval = setInterval(refresh, 60000);
    return () => clearInterval(interval);
  }, [refresh]);

  return status;
}

// ---------------------------------------------------------------------------
// Formatting helpers
// ---------------------------------------------------------------------------

function formatTokens(n: number): string {
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
  if (n >= 1_000) return `${(n / 1_000).toFixed(0)}K`;
  return String(n);
}

const STATUS_COLORS: Record<ProviderStatus, string> = {
  active: '#33CC66',
  configured: '#4FC3F7',
  'not-configured': '#666666',
  offline: '#FF4444',
};

const STATUS_LABELS: Record<ProviderStatus, string> = {
  active: 'active',
  configured: 'configured',
  'not-configured': 'not configured',
  offline: 'offline',
};

const STATUS_SYMBOLS: Record<ProviderStatus, string> = {
  active: '\u25CF',          // filled circle
  configured: '\u25D0',      // half circle
  'not-configured': '\u25CB', // empty circle
  offline: '\u2716',          // heavy X
};

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export const LLMStatus: React.FC = () => {
  const [providers, setProviders] = useState<LLMProvider[]>([]);
  const [collapsed, setCollapsed] = useState(false);
  const [refreshing, setRefreshing] = useState(false);

  const doRefresh = useCallback(async () => {
    setRefreshing(true);
    try {
      const result = await fetchLLMProviders();
      setProviders(result);
    } finally {
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    doRefresh();
    const interval = setInterval(doRefresh, 60000);
    return () => clearInterval(interval);
  }, [doRefresh]);

  const activeCount = providers.filter(p => p.status === 'active').length;
  const configuredCount = providers.filter(p => p.status === 'configured').length;

  // Session usage tracking
  const sessionUsage = useSessionUsage();
  const modelUsages = Array.from(sessionUsage.models.values());
  const totalRequests = modelUsages.reduce((sum, m) => sum + m.requestCount, 0);

  return (
    <div className="prime-radiant__llm-status">
      {/* Header row */}
      <div
        className="prime-radiant__llm-header"
        onClick={() => setCollapsed(!collapsed)}
        style={{ cursor: 'pointer' }}
      >
        <span className="prime-radiant__llm-title">
          LLM Providers
          <span style={{
            fontSize: '0.75em',
            marginLeft: 8,
            color: '#aaa',
          }}>
            {activeCount} active
            {configuredCount > 0 && ` \u00B7 ${configuredCount} ready`}
          </span>
          {/* Status dots summary */}
          <span style={{ marginLeft: 8 }}>
            {providers.map((p) => (
              <span
                key={p.name}
                className="prime-radiant__llm-dot"
                style={{ backgroundColor: STATUS_COLORS[p.status] }}
                title={`${p.name}: ${STATUS_LABELS[p.status]}`}
              />
            ))}
          </span>
        </span>
        <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          {/* Manual refresh button */}
          <span
            className="prime-radiant__llm-refresh"
            onClick={(e) => { e.stopPropagation(); doRefresh(); }}
            title="Refresh status"
            style={{
              cursor: 'pointer',
              opacity: refreshing ? 0.4 : 0.7,
              fontSize: '0.8em',
              transition: 'opacity 0.2s',
            }}
          >
            {refreshing ? '\u23F3' : '\u21BB'}
          </span>
          <span className="prime-radiant__llm-toggle">{collapsed ? '\u25B6' : '\u25BC'}</span>
        </span>
      </div>

      {/* Provider list — grouped by category */}
      {!collapsed && (
        <div className="prime-radiant__llm-list">
          {CATEGORY_ORDER.map((cat) => {
            const catProviders = providers.filter(p => p.category === cat);
            if (catProviders.length === 0) return null;
            // Aggregate worst status for category LED
            // Best status in group for LED
            const bestStatus = catProviders.some(p => p.status === 'active') ? 'active'
              : catProviders.some(p => p.status === 'configured') ? 'configured'
              : catProviders.some(p => p.status === 'offline') ? 'offline'
              : 'not-configured';

            return (
              <div key={cat} className="prime-radiant__llm-category">
                <div className="prime-radiant__llm-category-header">
                  <span className="prime-radiant__llm-category-label">{CATEGORY_LABELS[cat]}</span>
                  <span
                    className="prime-radiant__llm-category-led"
                    style={{ backgroundColor: STATUS_COLORS[bestStatus] }}
                    title={`${CATEGORY_LABELS[cat]}: ${STATUS_LABELS[bestStatus]}`}
                  />
                </div>
                {catProviders.map((p) => (
                  <div
                    key={p.name}
                    className="prime-radiant__llm-item"
                    title={p.statusHint ?? `${p.name}: ${STATUS_LABELS[p.status]}`}
                  >
                    <span className="prime-radiant__llm-icon" style={{
                      color: STATUS_COLORS[p.status],
                      minWidth: 22,
                      textAlign: 'center',
                    }}>{p.icon}</span>
                    <div className="prime-radiant__llm-info" style={{ flex: 1, minWidth: 0 }}>
                      <div className="prime-radiant__llm-model" style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: 6,
                      }}>
                        <span style={{ fontWeight: 600 }}>{p.name}</span>
                        <span className="prime-radiant__llm-badge" style={{
                          fontSize: '0.7em',
                          padding: '1px 6px',
                          borderRadius: 8,
                          backgroundColor: `${STATUS_COLORS[p.status]}22`,
                          color: STATUS_COLORS[p.status],
                          border: `1px solid ${STATUS_COLORS[p.status]}44`,
                          whiteSpace: 'nowrap',
                        }}>
                          {STATUS_SYMBOLS[p.status]} {STATUS_LABELS[p.status]}
                        </span>
                      </div>
                      <div className="prime-radiant__llm-meta" style={{
                        fontSize: '0.8em',
                        color: '#999',
                        overflow: 'hidden',
                        textOverflow: 'ellipsis',
                        whiteSpace: 'nowrap',
                      }}>
                        {p.detail}
                      </div>
                    </div>

                    {/* Usage bar for providers that track usage */}
                    {p.usage && p.usage.dailyLimit && p.usage.dailyLimit > 0 && (
                      <div className="prime-radiant__llm-usage" style={{ width: 80 }}>
                        <div className="prime-radiant__llm-usage-bar" style={{
                          height: 4,
                          backgroundColor: '#333',
                          borderRadius: 2,
                          overflow: 'hidden',
                        }}>
                          <div
                            className="prime-radiant__llm-usage-fill"
                            style={{
                              height: '100%',
                              width: `${Math.min(p.usage.percentUsed ?? 0, 100)}%`,
                              backgroundColor:
                                (p.usage.percentUsed ?? 0) > 80 ? '#FF4444'
                                : (p.usage.percentUsed ?? 0) > 50 ? '#FFB300'
                                : '#33CC66',
                              transition: 'width 0.3s',
                            }}
                          />
                        </div>
                        <div style={{ fontSize: '0.65em', color: '#888', textAlign: 'right' }}>
                          {formatTokens(p.usage.today)}/{formatTokens(p.usage.dailyLimit)}
                        </div>
                      </div>
                    )}
                  </div>
                ))}
              </div>
            );
          })}
        </div>
      )}

      {/* Session Usage Tracking */}
      {!collapsed && modelUsages.length > 0 && (
        <div className="prime-radiant__llm-session-usage">
          <div className="prime-radiant__llm-session-header">
            Session Usage
            <span style={{ fontSize: '0.75em', color: '#888', marginLeft: 8 }}>
              {totalRequests} request{totalRequests !== 1 ? 's' : ''}
            </span>
          </div>

          {/* Per-model usage bars */}
          {modelUsages.map((mu: ModelUsage) => {
            const quota = getQuotaPercent(mu.model);
            const hasQuota = quota >= 0;
            const isLow = hasQuota && quota < 10;
            const usedPercent = hasQuota ? 100 - quota : 0;

            return (
              <div
                key={mu.model}
                className={`prime-radiant__llm-usage-row${isLow ? ' prime-radiant__llm-usage-row--critical' : ''}`}
                title={`${mu.model}: ${formatTokens(mu.inputTokens)} in / ${formatTokens(mu.outputTokens)} out`}
              >
                <div className="prime-radiant__llm-usage-model-name">
                  {mu.model}
                </div>
                <div className="prime-radiant__llm-usage-bar-container">
                  {hasQuota ? (
                    <>
                      <div className="prime-radiant__llm-session-bar">
                        <div
                          className="prime-radiant__llm-session-bar-fill"
                          style={{
                            width: `${Math.min(usedPercent, 100)}%`,
                            backgroundColor:
                              usedPercent > 90 ? '#FF4444'
                              : usedPercent > 70 ? '#FFB300'
                              : '#33CC66',
                          }}
                        />
                      </div>
                      <span className="prime-radiant__llm-usage-pct">
                        {quota}% left
                      </span>
                    </>
                  ) : (
                    <span className="prime-radiant__llm-usage-tokens">
                      {formatTokens(mu.inputTokens)} in / {formatTokens(mu.outputTokens)} out
                    </span>
                  )}
                </div>
                <span className="prime-radiant__llm-usage-cost">
                  ${mu.estimatedCostUsd.toFixed(4)}
                </span>
                {isLow && (
                  <span className="prime-radiant__llm-usage-alert" title="Quota critically low">
                    !!
                  </span>
                )}
              </div>
            );
          })}

          {/* Total session cost */}
          <div className="prime-radiant__llm-session-total">
            <span>Session total</span>
            <span className="prime-radiant__llm-session-cost">
              ${sessionUsage.totalCostUsd.toFixed(4)} USD
            </span>
          </div>
        </div>
      )}
    </div>
  );
};
