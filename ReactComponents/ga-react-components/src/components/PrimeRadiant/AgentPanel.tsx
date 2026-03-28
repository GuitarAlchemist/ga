// src/components/PrimeRadiant/AgentPanel.tsx
// Shows active Claude Code agent teams and their members in the Prime Radiant

import React, { useEffect, useState, useCallback } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
export interface AgentInfo {
  id: string;
  name: string;
  type: string;  // e.g., 'general-purpose', 'Explore', 'Plan', 'code-review'
  status: 'running' | 'completed' | 'failed' | 'pending';
  task?: string;  // what the agent is working on
  startedAt?: string;
  duration?: string;
}

export interface AgentTeam {
  id: string;
  name: string;
  status: 'active' | 'completed' | 'disbanded';
  agents: AgentInfo[];
  createdAt?: string;
}

// ---------------------------------------------------------------------------
// Claude Code session types
// ---------------------------------------------------------------------------
export interface ClaudeSession {
  sessionId: string;
  model: string;
  branch: string;
  cwd: string;
  subagentCount: number;
  lastActiveAt: string;
  sizeBytes: number;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
function formatRelativeTime(iso: string): string {
  const diffMs = Date.now() - new Date(iso).getTime();
  const mins = Math.floor(diffMs / 60000);
  if (mins < 1) return 'just now';
  if (mins < 60) return `${mins}m ago`;
  const hours = Math.floor(mins / 60);
  if (hours < 24) return `${hours}h ago`;
  return `${Math.floor(hours / 24)}d ago`;
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1048576) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / 1048576).toFixed(1)} MB`;
}

function shortenPath(p: string): string {
  if (!p) return '';
  const segments = p.replace(/\\/g, '/').split('/').filter(Boolean);
  return segments.slice(-2).join('/');
}

function shortenModel(model: string): string {
  if (!model || model === 'unknown') return 'Claude';
  // e.g. "claude-opus-4-6[1m]" → "Opus 4.6"
  const m = model.match(/claude[- ]?(opus|sonnet|haiku)[- ]?(\d+)[- .]?(\d+)?/i);
  if (m) {
    const name = m[1].charAt(0).toUpperCase() + m[1].slice(1);
    return m[3] ? `${name} ${m[2]}.${m[3]}` : `${name} ${m[2]}`;
  }
  return model;
}

function isActiveRecent(iso: string): 'green' | 'amber' {
  const diffMs = Date.now() - new Date(iso).getTime();
  return diffMs < 5 * 60000 ? 'green' : 'amber';
}

// ---------------------------------------------------------------------------
// Data fetching
// ---------------------------------------------------------------------------
async function fetchClaudeSessions(): Promise<ClaudeSession[]> {
  try {
    const res = await fetch('/api/governance/claude-sessions');
    if (res.ok) {
      const data = await res.json();
      return data.sessions ?? [];
    }
  } catch { /* fall through */ }
  return [];
}

async function fetchAgentTeams(): Promise<AgentTeam[]> {
  // Try backend endpoint
  try {
    const res = await fetch('/api/agents/teams');
    if (res.ok) return await res.json();
  } catch { /* fall through */ }

  // Try local Claude Code state (if running in dev)
  try {
    const res = await fetch('/api/governance/agents');
    if (res.ok) {
      const data = await res.json();
      return data.teams ?? [];
    }
  } catch { /* fall through */ }

  // Fallback: representative agent teams when API is unavailable
  return [
    {
      id: 'team-governance',
      name: 'Governance Cycle',
      status: 'active',
      agents: [
        { id: 'a1', name: 'Demerzel', type: 'governance-scoped', status: 'running', task: 'Audit cycle 004 — policy compliance' },
        { id: 'a2', name: 'Skeptical Auditor', type: 'Explore', status: 'running', task: 'Red team defense scoring' },
        { id: 'a3', name: 'Seldon', type: 'Plan', status: 'completed', task: 'Research cycle — hexavalent logic' },
      ],
    },
    {
      id: 'team-build',
      name: 'Prime Radiant Sprint',
      status: 'active',
      agents: [
        { id: 'a4', name: 'Builder', type: 'general-purpose', status: 'running', task: 'Panel fallback data + LLM status' },
        { id: 'a5', name: 'Render Critic', type: 'code-review', status: 'pending', task: 'Visual verification pass' },
      ],
    },
  ];
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
const STATUS_COLOR: Record<AgentInfo['status'], string> = {
  running: '#33CC66',
  completed: '#4FC3F7',
  failed: '#FF4444',
  pending: '#888888',
};

const STATUS_ICON: Record<AgentInfo['status'], string> = {
  running: '*',
  completed: '+',
  failed: 'x',
  pending: '.',
};

export const AgentPanel: React.FC = () => {
  const [teams, setTeams] = useState<AgentTeam[]>([]);
  const [sessions, setSessions] = useState<ClaudeSession[]>([]);
  const [collapsed, setCollapsed] = useState(false);
  const [expandedTeams, setExpandedTeams] = useState<Set<string>>(new Set());

  useEffect(() => {
    fetchAgentTeams().then(setTeams);
    fetchClaudeSessions().then(setSessions);
    const teamInterval = setInterval(() => fetchAgentTeams().then(setTeams), 10000);
    const sessionInterval = setInterval(() => fetchClaudeSessions().then(setSessions), 30000);
    return () => { clearInterval(teamInterval); clearInterval(sessionInterval); };
  }, []);

  const toggleTeam = useCallback((teamId: string) => {
    setExpandedTeams(prev => {
      const next = new Set(prev);
      if (next.has(teamId)) next.delete(teamId);
      else next.add(teamId);
      return next;
    });
  }, []);

  const activeAgents = teams.reduce((sum, t) =>
    sum + t.agents.filter(a => a.status === 'running').length, 0);

  // Always render — never return null (causes empty panel)

  return (
    <div className="prime-radiant__agents">
      <div
        className="prime-radiant__agents-header"
        onClick={() => setCollapsed(!collapsed)}
      >
        <span className="prime-radiant__agents-title">
          Agents
          {activeAgents > 0 && (
            <span className="prime-radiant__agents-count prime-radiant__agents-count--active">
              {activeAgents} active
            </span>
          )}
          <span className="prime-radiant__agents-count">
            {teams.length} team{teams.length !== 1 ? 's' : ''}
          </span>
        </span>
        <span className="prime-radiant__agents-toggle">{collapsed ? '>' : 'v'}</span>
      </div>

      {!collapsed && (
        <div className="prime-radiant__agents-body">
          {/* Claude Code Sessions */}
          {sessions.length > 0 && (
            <div className="prime-radiant__agents-sessions">
              <div className="prime-radiant__agents-sessions-title">Claude Code Sessions</div>
              {sessions.map(s => {
                const activity = isActiveRecent(s.lastActiveAt);
                return (
                  <div key={s.sessionId} className="prime-radiant__agents-session">
                    <span
                      className="prime-radiant__agents-session-dot"
                      style={{ background: activity === 'green' ? '#33CC66' : '#F5A623' }}
                    />
                    <span className="prime-radiant__agents-session-icon">{'>_'}</span>
                    <span className="prime-radiant__agents-session-model">{shortenModel(s.model)}</span>
                    {s.branch && (
                      <span className="prime-radiant__agents-session-branch">{s.branch}</span>
                    )}
                    {s.cwd && (
                      <span className="prime-radiant__agents-session-cwd">{shortenPath(s.cwd)}</span>
                    )}
                    {s.subagentCount > 0 && (
                      <span className="prime-radiant__agents-session-badge">
                        {s.subagentCount} agent{s.subagentCount !== 1 ? 's' : ''}
                      </span>
                    )}
                    <span className="prime-radiant__agents-session-time">{formatRelativeTime(s.lastActiveAt)}</span>
                    <span className="prime-radiant__agents-session-size">{formatBytes(s.sizeBytes)}</span>
                  </div>
                );
              })}
            </div>
          )}

          {teams.map(team => (
            <div key={team.id} className="prime-radiant__agents-team">
              <div
                className="prime-radiant__agents-team-header"
                onClick={() => toggleTeam(team.id)}
              >
                <span className="prime-radiant__agents-team-status" style={{
                  color: team.status === 'active' ? '#33CC66' : '#484f58',
                }}>
                  {team.status === 'active' ? '*' : '-'}
                </span>
                <span className="prime-radiant__agents-team-name">{team.name}</span>
                <span className="prime-radiant__agents-team-count">
                  {team.agents.length}
                </span>
              </div>

              {expandedTeams.has(team.id) && (
                <div className="prime-radiant__agents-members">
                  {team.agents.map(agent => (
                    <div
                      key={agent.id}
                      className="prime-radiant__agents-member"
                      title={agent.task ?? agent.type}
                    >
                      <span style={{ color: STATUS_COLOR[agent.status], fontSize: 10, flexShrink: 0 }}>
                        {STATUS_ICON[agent.status]}
                      </span>
                      <span className="prime-radiant__agents-member-name">
                        {agent.name || agent.type}
                      </span>
                      {agent.task && (
                        <span className="prime-radiant__agents-member-task">{agent.task}</span>
                      )}
                      {agent.duration && (
                        <span className="prime-radiant__agents-member-time">{agent.duration}</span>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
