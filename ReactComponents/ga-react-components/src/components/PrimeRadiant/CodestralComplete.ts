// src/components/PrimeRadiant/CodestralComplete.ts
// Standalone code completion helper powered by Codestral API

import { useState, useCallback } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

interface ChatMessage {
  role: 'system' | 'user' | 'assistant';
  content: string;
}

interface ChatChoice {
  message: { content: string };
}

interface ChatResponse {
  choices: ChatChoice[];
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const CODESTRAL_ENDPOINT = '/proxy/codestral/v1/chat/completions';
const MODEL = 'codestral-latest';

const IXQL_SYSTEM_PROMPT = `You are an IXQL code completion engine for Prime Radiant governance visualization.
Complete the IXQL command that the user has started typing. Return ONLY the completed command, nothing else.

Commands:
- SELECT * WHERE health.staleness > 0.5 SET glow = true, color = "#FF4444"
- RESET
- CREATE PANEL "my-panel" KIND grid FROM "/api/data" COLUMNS name, status
- BIND HEALTH "panel-id" TO "/api/status" WHEN status = "error" THEN error
- SHOW tower | HIDE tower
- DIAGNOSE | HEALTH CHECK
- FIX errors | FIX signals | FIX all`;

// ---------------------------------------------------------------------------
// Core API
// ---------------------------------------------------------------------------

async function callCodestral(
  messages: ChatMessage[],
  temperature = 0.1,
  maxTokens = 256,
): Promise<string> {
  const res = await fetch(CODESTRAL_ENDPOINT, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      model: MODEL,
      messages,
      temperature,
      max_tokens: maxTokens,
    }),
  });

  if (!res.ok) {
    const text = await res.text();
    throw new Error(`Codestral API error ${res.status}: ${text}`);
  }

  const data: ChatResponse = await res.json();
  return data.choices[0]?.message?.content?.trim() ?? '';
}

// ---------------------------------------------------------------------------
// Public functions
// ---------------------------------------------------------------------------

/**
 * Complete arbitrary code in any language via Codestral.
 */
export async function completeCode(prefix: string, language: string): Promise<string> {
  const systemPrompt = `You are a code completion engine. Complete the following ${language} code. Return ONLY the completed code, no explanation.`;
  return callCodestral([
    { role: 'system', content: systemPrompt },
    { role: 'user', content: prefix },
  ]);
}

/**
 * Complete an IXQL command prefix using Codestral with grammar context.
 */
export async function completeIxql(prefix: string): Promise<string> {
  return callCodestral([
    { role: 'system', content: IXQL_SYSTEM_PROMPT },
    { role: 'user', content: prefix },
  ]);
}

// ---------------------------------------------------------------------------
// React hook
// ---------------------------------------------------------------------------

export interface UseCodeCompletionResult {
  complete: (prefix: string, language?: string) => Promise<void>;
  completing: boolean;
  result: string;
}

export function useCodeCompletion(): UseCodeCompletionResult {
  const [completing, setCompleting] = useState(false);
  const [result, setResult] = useState('');

  const complete = useCallback(async (prefix: string, language?: string) => {
    setCompleting(true);
    setResult('');
    try {
      const output = language
        ? await completeCode(prefix, language)
        : await completeIxql(prefix);
      setResult(output);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Completion failed';
      setResult(`// Error: ${msg}`);
    } finally {
      setCompleting(false);
    }
  }, []);

  return { complete, completing, result };
}
