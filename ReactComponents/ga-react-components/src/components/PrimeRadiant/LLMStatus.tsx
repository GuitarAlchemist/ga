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
// Mock data — TODO: connect to real API usage endpoints
// ---------------------------------------------------------------------------
function getLLMProviders(): LLMProvider[] {
  return [
    {
      name: 'Anthropic',
      icon: 'A',
      model: 'Claude Opus 4.6',
      plan: 'Max (1M ctx)',
      tokensUsed: '847K',
      tokensLimit: '1M',
      status: 'active',
    },
    {
      name: 'OpenAI',
      icon: 'O',
      model: 'GPT-4o',
      plan: 'Plus',
      creditsLeft: '$12.40',
      status: 'active',
    },
    {
      name: 'Google',
      icon: 'G',
      model: 'Gemini 2.5 Pro',
      plan: 'Free tier',
      tokensUsed: '45K',
      tokensLimit: '50K/day',
      status: 'limited',
    },
  ];
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
  const [collapsed, setCollapsed] = useState(true);

  useEffect(() => {
    setProviders(getLLMProviders());
    // TODO: poll real usage every 60s
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
