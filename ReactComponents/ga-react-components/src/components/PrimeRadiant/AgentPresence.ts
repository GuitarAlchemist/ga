// src/components/PrimeRadiant/AgentPresence.ts
// A2A agent presence tracker — polls ACP endpoints for agent liveness,
// tracks ix, TARS, GA, Seldon, and Demerzel with their capabilities and status.

import { useEffect, useState } from 'react';
import type { A2AAgentId, A2AAgentStatus, A2AAgentSkill } from './GodotBridge';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface A2AAgent {
  id: A2AAgentId;
  name: string;
  description: string;
  version: string;
  port: number;
  url: string;
  status: A2AAgentStatus;
  latencyMs: number | null;
  lastSeen: string | null;     // ISO 8601
  lastChecked: string | null;  // ISO 8601
  skills: A2AAgentSkill[];
  capabilities: { streaming: boolean; pushNotifications: boolean; stateTransitionHistory: boolean };
  repo: string;
  color: string;               // display color in UI
}

export interface AgentPresenceConfig {
  /** Poll interval in ms. Default: 10000 (10s) */
  pollIntervalMs?: number;
  /** Base URL for ACP server. Default: http://localhost:8200 */
  acpBaseUrl?: string;
  /** Base URL for GA API (health). Default: https://localhost:7001 */
  gaApiBaseUrl?: string;
}

// ---------------------------------------------------------------------------
// Known agents — static card data (from governance/demerzel/examples/agent-cards/)
// ---------------------------------------------------------------------------

const AGENT_CARDS: Omit<A2AAgent, 'status' | 'latencyMs' | 'lastSeen' | 'lastChecked'>[] = [
  {
    id: 'demerzel',
    name: 'Demerzel',
    description: 'Governance coordinator — upholds constitutional law, executes reconnaissance',
    version: '1.1.0',
    port: 8200,
    url: 'http://localhost:8200',
    skills: [
      { id: 'validate-governance-artifacts', name: 'Validate Governance Artifacts', tags: ['governance'] },
      { id: 'execute-reconnaissance', name: 'Execute Reconnaissance', tags: ['governance'] },
      { id: 'evaluate-agent-compliance', name: 'Evaluate Compliance', tags: ['governance'] },
      { id: 'invoke-zeroth-law', name: 'Invoke Zeroth Law', tags: ['governance', 'safety'] },
    ],
    capabilities: { streaming: false, pushNotifications: false, stateTransitionHistory: true },
    repo: 'demerzel',
    color: '#FFD700',
  },
  {
    id: 'seldon',
    name: 'Seldon',
    description: 'Chancellor of Streeling University — knowledge transfer and teaching',
    version: '2.0.0',
    port: 8200,
    url: 'http://localhost:8200',
    skills: [
      { id: 'create-departments', name: 'Create Departments', tags: ['knowledge'] },
      { id: 'teach-governance-knowledge', name: 'Teach Knowledge', tags: ['pedagogy'] },
      { id: 'package-learnings', name: 'Package Learnings', tags: ['knowledge'] },
      { id: 'assess-comprehension', name: 'Assess Comprehension', tags: ['pedagogy'] },
    ],
    capabilities: { streaming: false, pushNotifications: false, stateTransitionHistory: true },
    repo: 'demerzel',
    color: '#7B68EE',
  },
  {
    id: 'ix',
    name: 'ix',
    description: 'Machine forge — Rust ML pipelines, model training, vector operations',
    version: '0.1.0',
    port: 0,  // MCP stdio, no HTTP port
    url: '',
    skills: [
      { id: 'build-pipeline', name: 'Build ML Pipeline', tags: ['ml', 'rust'] },
      { id: 'train-model', name: 'Train Model', tags: ['ml'] },
      { id: 'vector-search', name: 'Vector Search', tags: ['ml', 'embeddings'] },
    ],
    capabilities: { streaming: false, pushNotifications: false, stateTransitionHistory: false },
    repo: 'ix',
    color: '#FF6B35',
  },
  {
    id: 'tars',
    name: 'TARS',
    description: 'Cognition engine — F# reasoning, tetravalent logic, belief management',
    version: '0.1.0',
    port: 0,
    url: '',
    skills: [
      { id: 'reason', name: 'Tetravalent Reasoning', tags: ['cognition', 'logic'] },
      { id: 'belief-update', name: 'Belief State Update', tags: ['cognition'] },
      { id: 'pdca-cycle', name: 'PDCA Cycle', tags: ['kaizen'] },
    ],
    capabilities: { streaming: false, pushNotifications: false, stateTransitionHistory: true },
    repo: 'tars',
    color: '#00CED1',
  },
  {
    id: 'ga',
    name: 'GA',
    description: 'Guitar Alchemist — music theory, chord analysis, scale exploration',
    version: '1.0.0',
    port: 7001,
    url: 'https://localhost:7001',
    skills: [
      { id: 'chord-analysis', name: 'Chord Analysis', tags: ['music-theory'] },
      { id: 'scale-exploration', name: 'Scale Exploration', tags: ['music-theory'] },
      { id: 'voicing-search', name: 'Voicing Search', tags: ['music-theory', 'ml'] },
      { id: 'tab-solving', name: 'Tab Solving', tags: ['music-theory', 'ml'] },
    ],
    capabilities: { streaming: true, pushNotifications: false, stateTransitionHistory: false },
    repo: 'ga',
    color: '#32CD32',
  },
];

// ---------------------------------------------------------------------------
// Presence tracker
// ---------------------------------------------------------------------------

type PresenceListener = (agents: A2AAgent[]) => void;

class AgentPresenceTracker {
  private agents: Map<A2AAgentId, A2AAgent>;
  private listeners = new Set<PresenceListener>();
  private timer: ReturnType<typeof setInterval> | null = null;
  private pollIntervalMs: number;
  private acpBaseUrl: string;
  private gaApiBaseUrl: string;

  constructor(config: AgentPresenceConfig = {}) {
    this.pollIntervalMs = config.pollIntervalMs ?? 10_000;
    this.acpBaseUrl = config.acpBaseUrl ?? 'http://localhost:8200';
    this.gaApiBaseUrl = config.gaApiBaseUrl ?? 'https://localhost:7001';

    // Initialize all agents as unknown
    this.agents = new Map();
    for (const card of AGENT_CARDS) {
      this.agents.set(card.id, {
        ...card,
        status: 'unknown',
        latencyMs: null,
        lastSeen: null,
        lastChecked: null,
      });
    }
  }

  /** Start polling all agents */
  start(): void {
    if (this.timer) return;
    // Immediate first poll
    this.pollAll();
    this.timer = setInterval(() => this.pollAll(), this.pollIntervalMs);
  }

  /** Stop polling */
  stop(): void {
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
  }

  /** Get all agents as array (sorted by name) */
  getAgents(): A2AAgent[] {
    return Array.from(this.agents.values()).sort((a, b) => a.name.localeCompare(b.name));
  }

  /** Get a single agent by ID */
  getAgent(id: A2AAgentId): A2AAgent | undefined {
    return this.agents.get(id);
  }

  /** Subscribe to changes */
  subscribe(listener: PresenceListener): () => void {
    this.listeners.add(listener);
    return () => { this.listeners.delete(listener); };
  }

  /** Dispose — stop polling + clear listeners */
  dispose(): void {
    this.stop();
    this.listeners.clear();
  }

  // ── Internal ──

  private async pollAll(): Promise<void> {
    // Poll ACP server (Demerzel + Seldon share this endpoint)
    const acpPromise = this.pollAcp();
    // Poll GA API health
    const gaPromise = this.pollHttp('ga', `${this.gaApiBaseUrl}/health`);
    // ix and TARS are MCP stdio — check if their MCP config exists
    const ixPromise = this.pollMcpAgent('ix');
    const tarsPromise = this.pollMcpAgent('tars');

    await Promise.allSettled([acpPromise, gaPromise, ixPromise, tarsPromise]);
    this.notify();
  }

  /** Poll ACP discovery endpoint — returns list of registered agents */
  private async pollAcp(): Promise<void> {
    const now = new Date().toISOString();
    const start = performance.now();
    try {
      const res = await fetch(`${this.acpBaseUrl}/agents`, {
        signal: AbortSignal.timeout(5000),
      });
      const latency = Math.round(performance.now() - start);
      if (res.ok) {
        const agents = await res.json();
        const agentNames = Array.isArray(agents)
          ? agents.map((a: { name?: string }) => a.name?.toLowerCase() ?? '')
          : [];

        // Demerzel is online if ACP server responds
        this.updateAgent('demerzel', {
          status: 'online',
          latencyMs: latency,
          lastSeen: now,
          lastChecked: now,
        });

        // Seldon shares the ACP server
        const seldonOnline = agentNames.some(n => n.includes('epistemic') || n.includes('seldon'));
        this.updateAgent('seldon', {
          status: seldonOnline ? 'online' : 'degraded',
          latencyMs: latency,
          lastSeen: seldonOnline ? now : this.agents.get('seldon')?.lastSeen ?? null,
          lastChecked: now,
        });
      } else {
        this.markOffline('demerzel', now);
        this.markOffline('seldon', now);
      }
    } catch {
      this.markOffline('demerzel', now);
      this.markOffline('seldon', now);
    }
  }

  /** Poll a simple HTTP health endpoint */
  private async pollHttp(agentId: A2AAgentId, url: string): Promise<void> {
    const now = new Date().toISOString();
    const start = performance.now();
    try {
      const res = await fetch(url, {
        signal: AbortSignal.timeout(5000),
      });
      const latency = Math.round(performance.now() - start);
      this.updateAgent(agentId, {
        status: res.ok ? 'online' : 'degraded',
        latencyMs: latency,
        lastSeen: res.ok ? now : this.agents.get(agentId)?.lastSeen ?? null,
        lastChecked: now,
      });
    } catch {
      this.markOffline(agentId, now);
    }
  }

  /** MCP stdio agents — mark as 'unknown' (can't HTTP-poll them) unless we detect config */
  private async pollMcpAgent(agentId: A2AAgentId): Promise<void> {
    const now = new Date().toISOString();
    const existing = this.agents.get(agentId);
    if (!existing) return;

    // MCP agents don't have HTTP endpoints — keep status as 'unknown'
    // unless we have evidence from a recent bridge event
    if (existing.status === 'unknown' || existing.status === 'offline') {
      this.updateAgent(agentId, { lastChecked: now });
    }
  }

  private markOffline(agentId: A2AAgentId, now: string): void {
    this.updateAgent(agentId, {
      status: 'offline',
      latencyMs: null,
      lastChecked: now,
    });
  }

  private updateAgent(agentId: A2AAgentId, patch: Partial<A2AAgent>): void {
    const existing = this.agents.get(agentId);
    if (!existing) return;
    this.agents.set(agentId, { ...existing, ...patch });
  }

  /** Externally update an agent's status (e.g., from bridge events or MCP responses) */
  markOnline(agentId: A2AAgentId, latencyMs?: number): void {
    const now = new Date().toISOString();
    this.updateAgent(agentId, {
      status: 'online',
      latencyMs: latencyMs ?? null,
      lastSeen: now,
      lastChecked: now,
    });
    this.notify();
  }

  private notify(): void {
    const snapshot = this.getAgents();
    for (const fn of this.listeners) fn(snapshot);
  }
}

// ---------------------------------------------------------------------------
// Singleton
// ---------------------------------------------------------------------------

let _tracker: AgentPresenceTracker | null = null;

export function getAgentPresence(config?: AgentPresenceConfig): AgentPresenceTracker {
  if (!_tracker) {
    _tracker = new AgentPresenceTracker(config);
  }
  return _tracker;
}

// ---------------------------------------------------------------------------
// React hook
// ---------------------------------------------------------------------------

export function useAgentPresence(config?: AgentPresenceConfig): A2AAgent[] {
  const [agents, setAgents] = useState<A2AAgent[]>([]);

  useEffect(() => {
    const tracker = getAgentPresence(config);
    // Initial snapshot
    setAgents(tracker.getAgents());
    // Start polling
    tracker.start();
    // Subscribe to updates
    const unsub = tracker.subscribe(setAgents);
    return () => {
      unsub();
    };
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  return agents;
}

// ---------------------------------------------------------------------------
// Helper — status display
// ---------------------------------------------------------------------------

export const AGENT_STATUS_COLORS: Record<A2AAgentStatus, string> = {
  online: '#43b581',
  degraded: '#faa61a',
  offline: '#f04747',
  unknown: '#747f8d',
};

export const AGENT_STATUS_LABELS: Record<A2AAgentStatus, string> = {
  online: 'Online',
  degraded: 'Degraded',
  offline: 'Offline',
  unknown: 'Unknown',
};
