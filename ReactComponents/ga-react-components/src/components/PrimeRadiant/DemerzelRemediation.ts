// src/components/PrimeRadiant/DemerzelRemediation.ts
// Auto-remediation bridge — connects algedonic signals to Demerzel ACP agents.
// Routes signals through risk assessment → ACP action → status update.

import { useCallback, useState } from 'react';
import type { AlgedonicSignal, AlgedonicSeverity } from './AlgedonicPanel';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export type RemediationRisk = 'low' | 'medium' | 'high';

export type RemediationAction =
  | 'auto_remediate'          // Demerzel fixes autonomously
  | 'auto_remediate_notify'   // Fix + notify via discussion
  | 'escalate'                // Requires human confirmation
  | 'investigate';            // Needs more info first

export interface RemediationResult {
  signalId: string;
  action: RemediationAction;
  risk: RemediationRisk;
  status: 'pending' | 'running' | 'success' | 'failed' | 'escalated';
  agentUsed: string;
  response?: string;
  error?: string;
  durationMs?: number;
  timestamp: string;
}

export interface RemediationState {
  results: Map<string, RemediationResult>;
  remediating: boolean;
}

// ---------------------------------------------------------------------------
// Risk classification (mirrors auto-remediation-policy.yaml)
// ---------------------------------------------------------------------------

const SIGNAL_RISK_MAP: Record<string, RemediationRisk> = {
  // Structural gaps — low risk, clear fix
  'missing_test': 'low',
  'missing_schema': 'low',
  'missing_state_dir': 'low',
  'stale_belief': 'low',
  // Staleness/coverage — medium risk
  'belief_stale': 'medium',
  'governance_drift': 'medium',
  'lolli_inflation': 'medium',
  'coverage_gap': 'medium',
  'model_degraded': 'medium',
  'policy_violation': 'medium',
  // Compliance/critical — high risk
  'belief_collapse': 'high',
  'constitutional_violation': 'high',
  'ci_failure': 'high',
  'security_gap': 'high',
};

const SEVERITY_RISK: Record<AlgedonicSeverity, RemediationRisk> = {
  info: 'low',
  warning: 'medium',
  emergency: 'high',
};

function classifyRisk(signal: AlgedonicSignal): RemediationRisk {
  // Named signal takes priority, then severity
  return SIGNAL_RISK_MAP[signal.signal] ?? SEVERITY_RISK[signal.severity] ?? 'medium';
}

function riskToAction(risk: RemediationRisk): RemediationAction {
  switch (risk) {
    case 'low': return 'auto_remediate';
    case 'medium': return 'auto_remediate_notify';
    case 'high': return 'escalate';
  }
}

function riskToAgent(risk: RemediationRisk, signal: AlgedonicSignal): string {
  // Route to most appropriate ACP agent based on signal type
  if (signal.signal.includes('belief') || signal.signal.includes('epistemic')) {
    return 'demerzel-epistemic';
  }
  if (signal.signal.includes('ci_') || signal.signal.includes('pipeline')) {
    return 'demerzel-whats-next';
  }
  if (risk === 'high') {
    return 'demerzel-governance'; // High risk → governance agent for constitutional review
  }
  return 'demerzel-pipeline'; // Default → pipeline agent for structured remediation
}

// ---------------------------------------------------------------------------
// ACP invocation
// ---------------------------------------------------------------------------

async function invokeAcpAgent(
  agentName: string,
  message: string,
): Promise<{ content: string; durationMs: number }> {
  const start = performance.now();

  const res = await fetch(`/proxy/acp/agents/${agentName}/run`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      input: [{ parts: [{ content: message }] }],
    }),
    signal: AbortSignal.timeout(30000),
  });

  const durationMs = Math.round(performance.now() - start);

  if (!res.ok) {
    const text = await res.text().catch(() => 'Unknown error');
    throw new Error(`ACP ${agentName} returned ${res.status}: ${text}`);
  }

  const data = await res.json();
  // ACP response format: { output: [{ parts: [{ content: "..." }] }] }
  const content = data?.output?.[0]?.parts?.[0]?.content
    ?? data?.message ?? JSON.stringify(data);

  return { content, durationMs };
}

// ---------------------------------------------------------------------------
// Remediate a single signal
// ---------------------------------------------------------------------------

export async function remediateSignal(signal: AlgedonicSignal): Promise<RemediationResult> {
  const risk = classifyRisk(signal);
  const action = riskToAction(risk);
  const agentName = riskToAgent(risk, signal);

  const result: RemediationResult = {
    signalId: signal.id,
    action,
    risk,
    status: 'running',
    agentUsed: agentName,
    timestamp: new Date().toISOString(),
  };

  // High risk → escalate immediately, don't auto-execute
  if (action === 'escalate') {
    return {
      ...result,
      status: 'escalated',
      response: `Signal "${signal.signal}" classified as HIGH RISK. Requires human confirmation before remediation. Agent ${agentName} ready to execute on approval.`,
    };
  }

  // Low/medium → invoke ACP agent
  const prompt = buildRemediationPrompt(signal, risk, action);

  try {
    const { content, durationMs } = await invokeAcpAgent(agentName, prompt);
    return {
      ...result,
      status: 'success',
      response: content,
      durationMs,
    };
  } catch (err) {
    return {
      ...result,
      status: 'failed',
      error: err instanceof Error ? err.message : String(err),
    };
  }
}

function buildRemediationPrompt(
  signal: AlgedonicSignal,
  risk: RemediationRisk,
  action: RemediationAction,
): string {
  return [
    `ALGEDONIC SIGNAL REMEDIATION REQUEST`,
    ``,
    `Signal: ${signal.signal}`,
    `Type: ${signal.type}`,
    `Severity: ${signal.severity}`,
    `Source: ${signal.source}`,
    `Description: ${signal.description ?? 'No description'}`,
    `Risk: ${risk}`,
    `Action: ${action}`,
    ``,
    `Please analyze this signal and ${action === 'auto_remediate'
      ? 'provide the fix. List specific files to create/modify and the changes needed.'
      : 'provide the fix with a summary for the governance discussion notification.'}`,
    ``,
    `Follow the auto-remediation policy: fixes must be reversible, proportional, and within bounded autonomy.`,
  ].join('\n');
}

// ---------------------------------------------------------------------------
// React hook
// ---------------------------------------------------------------------------

export interface UseRemediationResult {
  remediate: (signal: AlgedonicSignal) => Promise<RemediationResult>;
  remediateAll: (signals: AlgedonicSignal[]) => Promise<RemediationResult[]>;
  results: RemediationResult[];
  remediating: boolean;
  clearResults: () => void;
  getResult: (signalId: string) => RemediationResult | undefined;
}

export function useRemediation(): UseRemediationResult {
  const [results, setResults] = useState<RemediationResult[]>([]);
  const [remediating, setRemediating] = useState(false);

  const remediate = useCallback(async (signal: AlgedonicSignal): Promise<RemediationResult> => {
    setRemediating(true);
    try {
      const result = await remediateSignal(signal);
      setResults(prev => [result, ...prev.filter(r => r.signalId !== signal.id)]);
      return result;
    } finally {
      setRemediating(false);
    }
  }, []);

  const remediateAll = useCallback(async (signals: AlgedonicSignal[]): Promise<RemediationResult[]> => {
    setRemediating(true);
    try {
      // Process in parallel but respect risk ordering
      const sorted = [...signals].sort((a, b) => {
        const riskOrder: Record<RemediationRisk, number> = { low: 0, medium: 1, high: 2 };
        return riskOrder[classifyRisk(a)] - riskOrder[classifyRisk(b)];
      });

      const allResults = await Promise.all(sorted.map(remediateSignal));
      setResults(prev => {
        const ids = new Set(allResults.map(r => r.signalId));
        return [...allResults, ...prev.filter(r => !ids.has(r.signalId))];
      });
      return allResults;
    } finally {
      setRemediating(false);
    }
  }, []);

  const clearResults = useCallback(() => setResults([]), []);

  const getResult = useCallback(
    (signalId: string) => results.find(r => r.signalId === signalId),
    [results],
  );

  return { remediate, remediateAll, results, remediating, clearResults, getResult };
}
