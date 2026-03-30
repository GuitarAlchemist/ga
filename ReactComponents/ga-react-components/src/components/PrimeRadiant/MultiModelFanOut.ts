// src/components/PrimeRadiant/MultiModelFanOut.ts
// Layer 1 multi-model fan-out service — sends the same prompt to N LLM providers
// in parallel and collects results with timing metadata.

import { useState, useCallback } from 'react';

// ---------------------------------------------------------------------------
// Provider types
// ---------------------------------------------------------------------------

export type ProviderCategory = 'cloud' | 'local' | 'tools';

export interface ModelProvider {
  id: string;
  name: string;
  category: ProviderCategory;
  endpoint: ProviderEndpoint;
}

export type ProviderEndpoint =
  | { type: 'ollama'; proxyPath: string; model: string }
  | { type: 'mistral-agent'; url: string; agentId: string }
  | { type: 'codestral'; url: string }
  | { type: 'backend-proxy'; path: string; provider: string; model: string };

// ---------------------------------------------------------------------------
// Request / Response
// ---------------------------------------------------------------------------

export interface FanOutRequest {
  prompt: string;
  selectedProviders: string[];   // ModelProvider.id values
  temperature?: number;
  maxTokens?: number;
}

export type FanOutResultStatus = 'success' | 'error' | 'timeout';

export interface FanOutResult {
  model: string;                 // ModelProvider.id
  modelName: string;             // human-readable name
  content: string;
  latencyMs: number;
  status: FanOutResultStatus;
  error?: string;
}

export interface FanOutResponse {
  results: FanOutResult[];
  totalLatencyMs: number;
}

// ---------------------------------------------------------------------------
// Built-in provider catalog
// ---------------------------------------------------------------------------

export const MODEL_PROVIDERS: ModelProvider[] = [
  // ── Cloud (proxied through backend to keep keys server-side) ──
  {
    id: 'anthropic-claude',
    name: 'Claude (Anthropic)',
    category: 'cloud',
    endpoint: { type: 'backend-proxy', path: '/api/llm/chat', provider: 'anthropic', model: 'claude-sonnet-4-20250514' },
  },
  {
    id: 'openai-gpt4o',
    name: 'GPT-4o (OpenAI)',
    category: 'cloud',
    endpoint: { type: 'backend-proxy', path: '/api/llm/chat', provider: 'openai', model: 'gpt-4o' },
  },
  {
    id: 'gemini-pro',
    name: 'Gemini 2.5 Pro',
    category: 'cloud',
    endpoint: { type: 'backend-proxy', path: '/api/llm/chat', provider: 'gemini', model: 'gemini-2.5-pro' },
  },
  {
    id: 'mistral-ga-agent',
    name: 'Mistral GA Agent',
    category: 'cloud',
    endpoint: {
      type: 'mistral-agent',
      url: 'https://api.mistral.ai/v1/agents/completions',
      agentId: 'ag_019d3c30528e716fa8a5efeb9c8ae49c',
    },
  },

  // ── Local ──
  {
    id: 'ollama-llama3',
    name: 'Ollama (llama3)',
    category: 'local',
    endpoint: { type: 'ollama', proxyPath: '/proxy/ollama/api/chat', model: 'llama3' },
  },
  {
    id: 'ollama-mistral',
    name: 'Ollama (mistral)',
    category: 'local',
    endpoint: { type: 'ollama', proxyPath: '/proxy/ollama/api/chat', model: 'mistral' },
  },

  // ── Tools ──
  {
    id: 'codestral',
    name: 'Codestral',
    category: 'tools',
    endpoint: { type: 'codestral', url: 'https://codestral.mistral.ai/v1/chat/completions' },
  },
];

// ---------------------------------------------------------------------------
// Per-provider adapter functions
// ---------------------------------------------------------------------------

interface AdapterInput {
  prompt: string;
  temperature: number;
  maxTokens: number;
}

interface AdapterOutput {
  url: string;
  init: RequestInit;
  extractContent: (json: unknown) => string;
}

function buildOllamaRequest(ep: Extract<ProviderEndpoint, { type: 'ollama' }>, input: AdapterInput): AdapterOutput {
  return {
    url: ep.proxyPath,
    init: {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        model: ep.model,
        messages: [{ role: 'user', content: input.prompt }],
        stream: false,
        options: { temperature: input.temperature, num_predict: input.maxTokens },
      }),
    },
    extractContent: (json: unknown) => {
      const data = json as { message?: { content?: string } };
      return data?.message?.content ?? '';
    },
  };
}

function buildMistralAgentRequest(ep: Extract<ProviderEndpoint, { type: 'mistral-agent' }>, input: AdapterInput): AdapterOutput {
  const apiKey = (typeof import.meta !== 'undefined' && (import.meta as unknown as { env?: Record<string, string> }).env?.VITE_MISTRAL_API_KEY) ?? '';
  return {
    url: ep.url,
    init: {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(apiKey ? { Authorization: `Bearer ${apiKey}` } : {}),
      },
      body: JSON.stringify({
        agent_id: ep.agentId,
        messages: [{ role: 'user', content: input.prompt }],
        max_tokens: input.maxTokens,
        temperature: input.temperature,
      }),
    },
    extractContent: (json: unknown) => {
      const data = json as { choices?: Array<{ message?: { content?: string } }> };
      return data?.choices?.[0]?.message?.content ?? '';
    },
  };
}

function buildCodestralRequest(ep: Extract<ProviderEndpoint, { type: 'codestral' }>, input: AdapterInput): AdapterOutput {
  const apiKey = (typeof import.meta !== 'undefined' && (import.meta as unknown as { env?: Record<string, string> }).env?.VITE_CODESTRAL_API_KEY) ?? '';
  return {
    url: ep.url,
    init: {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(apiKey ? { Authorization: `Bearer ${apiKey}` } : {}),
      },
      body: JSON.stringify({
        model: 'codestral-latest',
        messages: [{ role: 'user', content: input.prompt }],
        max_tokens: input.maxTokens,
        temperature: input.temperature,
      }),
    },
    extractContent: (json: unknown) => {
      const data = json as { choices?: Array<{ message?: { content?: string } }> };
      return data?.choices?.[0]?.message?.content ?? '';
    },
  };
}

function buildBackendProxyRequest(ep: Extract<ProviderEndpoint, { type: 'backend-proxy' }>, input: AdapterInput): AdapterOutput {
  return {
    url: ep.path,
    init: {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        provider: ep.provider,
        model: ep.model,
        messages: [{ role: 'user', content: input.prompt }],
        maxTokens: input.maxTokens,
        temperature: input.temperature,
      }),
    },
    extractContent: (json: unknown) => {
      const data = json as { content?: string; choices?: Array<{ message?: { content?: string } }> };
      // Support both flat { content } and OpenAI-style { choices }
      return data?.content ?? data?.choices?.[0]?.message?.content ?? '';
    },
  };
}

function buildAdapterRequest(endpoint: ProviderEndpoint, input: AdapterInput): AdapterOutput {
  switch (endpoint.type) {
    case 'ollama':
      return buildOllamaRequest(endpoint, input);
    case 'mistral-agent':
      return buildMistralAgentRequest(endpoint, input);
    case 'codestral':
      return buildCodestralRequest(endpoint, input);
    case 'backend-proxy':
      return buildBackendProxyRequest(endpoint, input);
  }
}

// ---------------------------------------------------------------------------
// Core fan-out function
// ---------------------------------------------------------------------------

const DEFAULT_TIMEOUT_MS = 30_000;
const DEFAULT_TEMPERATURE = 0.7;
const DEFAULT_MAX_TOKENS = 1024;

async function callProvider(provider: ModelProvider, input: AdapterInput): Promise<FanOutResult> {
  const start = performance.now();
  try {
    const adapter = buildAdapterRequest(provider.endpoint, input);
    const response = await fetch(adapter.url, {
      ...adapter.init,
      signal: AbortSignal.timeout(DEFAULT_TIMEOUT_MS),
    });

    const latencyMs = Math.round(performance.now() - start);

    if (!response.ok) {
      const errorText = await response.text().catch(() => response.statusText);
      return {
        model: provider.id,
        modelName: provider.name,
        content: '',
        latencyMs,
        status: 'error',
        error: `HTTP ${response.status}: ${errorText}`,
      };
    }

    const json: unknown = await response.json();
    const content = adapter.extractContent(json);

    return {
      model: provider.id,
      modelName: provider.name,
      content,
      latencyMs,
      status: 'success',
    };
  } catch (err: unknown) {
    const latencyMs = Math.round(performance.now() - start);
    const isTimeout = err instanceof DOMException && err.name === 'TimeoutError';
    const message = err instanceof Error ? err.message : String(err);
    return {
      model: provider.id,
      modelName: provider.name,
      content: '',
      latencyMs,
      status: isTimeout ? 'timeout' : 'error',
      error: message,
    };
  }
}

export async function fanOutQuery(request: FanOutRequest): Promise<FanOutResponse> {
  const start = performance.now();
  const temperature = request.temperature ?? DEFAULT_TEMPERATURE;
  const maxTokens = request.maxTokens ?? DEFAULT_MAX_TOKENS;
  const input: AdapterInput = { prompt: request.prompt, temperature, maxTokens };

  // Resolve selected providers to their definitions
  const providerMap = new Map(MODEL_PROVIDERS.map((p) => [p.id, p]));
  const selected = request.selectedProviders
    .map((id) => providerMap.get(id))
    .filter((p): p is ModelProvider => p !== undefined);

  if (selected.length === 0) {
    return { results: [], totalLatencyMs: 0 };
  }

  // Fire all in parallel — one failure does not kill others
  const settled = await Promise.allSettled(
    selected.map((provider) => callProvider(provider, input)),
  );

  const results: FanOutResult[] = settled.map((outcome, i) => {
    if (outcome.status === 'fulfilled') {
      return outcome.value;
    }
    // Promise.allSettled rejection (should not happen — callProvider catches internally)
    return {
      model: selected[i].id,
      modelName: selected[i].name,
      content: '',
      latencyMs: Math.round(performance.now() - start),
      status: 'error' as const,
      error: outcome.reason instanceof Error ? outcome.reason.message : String(outcome.reason),
    };
  });

  return {
    results,
    totalLatencyMs: Math.round(performance.now() - start),
  };
}

// ---------------------------------------------------------------------------
// Helper — get provider by id
// ---------------------------------------------------------------------------

export function getProvider(id: string): ModelProvider | undefined {
  return MODEL_PROVIDERS.find((p) => p.id === id);
}

export function getProvidersByCategory(category: ProviderCategory): ModelProvider[] {
  return MODEL_PROVIDERS.filter((p) => p.category === category);
}

// ---------------------------------------------------------------------------
// React hook — useMultiModelQuery
// ---------------------------------------------------------------------------

export interface MultiModelQueryState {
  loading: boolean;
  response: FanOutResponse | null;
  error: string | null;
}

export interface UseMultiModelQueryResult extends MultiModelQueryState {
  execute: (request: FanOutRequest) => Promise<FanOutResponse>;
  reset: () => void;
}

export function useMultiModelQuery(): UseMultiModelQueryResult {
  const [state, setState] = useState<MultiModelQueryState>({
    loading: false,
    response: null,
    error: null,
  });

  const execute = useCallback(async (request: FanOutRequest): Promise<FanOutResponse> => {
    setState({ loading: true, response: null, error: null });
    try {
      const response = await fanOutQuery(request);
      setState({ loading: false, response, error: null });
      return response;
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      setState({ loading: false, response: null, error: message });
      throw err;
    }
  }, []);

  const reset = useCallback(() => {
    setState({ loading: false, response: null, error: null });
  }, []);

  return { ...state, execute, reset };
}
