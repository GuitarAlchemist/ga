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
// Data fetching — polls /api/agents or reads from local state
// ---------------------------------------------------------------------------
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
  const [collapsed, setCollapsed] = useState(false);
  const [expandedTeams, setExpandedTeams] = useState<Set<string>>(new Set());

  useEffect(() => {
    fetchAgentTeams().then(setTeams);
    const interval = setInterval(() => fetchAgentTeams().then(setTeams), 10000); // 10s refresh
    return () => clearInterval(interval);
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
