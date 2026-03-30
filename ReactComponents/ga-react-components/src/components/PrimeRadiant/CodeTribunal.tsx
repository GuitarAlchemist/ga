// src/components/PrimeRadiant/CodeTribunal.tsx
// Multi-model code generation and review panel — queries Ollama and Codestral in parallel.

import React, { useCallback, useRef, useState } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

type Language = 'csharp' | 'fsharp' | 'typescript' | 'python';
type Mode = 'generate' | 'review' | 'refactor';

interface ModelResult {
  model: string;
  code: string;
  latencyMs: number;
  error?: string;
}

interface DiffLine {
  type: 'same' | 'added' | 'removed';
  text: string;
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const LANGUAGE_OPTIONS: { value: Language; label: string }[] = [
  { value: 'csharp', label: 'C#' },
  { value: 'fsharp', label: 'F#' },
  { value: 'typescript', label: 'TypeScript' },
  { value: 'python', label: 'Python' },
];

const MODE_OPTIONS: { value: Mode; label: string }[] = [
  { value: 'generate', label: 'Generate' },
  { value: 'review', label: 'Review' },
  { value: 'refactor', label: 'Refactor' },
];

const SYSTEM_PROMPT = 'You are an expert programmer. Generate clean, well-documented code. Return only the code block, no explanation.';

const LANGUAGE_NAMES: Record<Language, string> = {
  csharp: 'C#',
  fsharp: 'F#',
  typescript: 'TypeScript',
  python: 'Python',
};

// ---------------------------------------------------------------------------
// Keyword highlighting — lightweight, no external deps
// ---------------------------------------------------------------------------

const KEYWORD_COLORS: Record<string, string> = {
  // C# / F# / TS shared
  'class': '#c084fc', 'interface': '#c084fc', 'enum': '#c084fc', 'struct': '#c084fc',
  'public': '#ff7b72', 'private': '#ff7b72', 'protected': '#ff7b72', 'internal': '#ff7b72',
  'static': '#ff7b72', 'abstract': '#ff7b72', 'override': '#ff7b72', 'virtual': '#ff7b72',
  'async': '#ff7b72', 'await': '#ff7b72', 'return': '#ff7b72', 'yield': '#ff7b72',
  'if': '#ff7b72', 'else': '#ff7b72', 'for': '#ff7b72', 'while': '#ff7b72',
  'let': '#79c0ff', 'const': '#79c0ff', 'var': '#79c0ff', 'val': '#79c0ff',
  'function': '#d2a8ff', 'def': '#d2a8ff', 'fun': '#d2a8ff', 'fn': '#d2a8ff',
  'import': '#ffa657', 'from': '#ffa657', 'open': '#ffa657', 'using': '#ffa657',
  'type': '#c084fc', 'record': '#c084fc', 'namespace': '#c084fc', 'module': '#c084fc',
  'true': '#79c0ff', 'false': '#79c0ff', 'null': '#79c0ff', 'None': '#79c0ff',
  // Python
  'self': '#ff7b72', 'cls': '#ff7b72', 'lambda': '#d2a8ff',
};

function highlightCode(code: string): React.ReactNode[] {
  return code.split('\n').map((line, i) => {
    const parts: React.ReactNode[] = [];
    // Simple word-boundary tokenizer
    const regex = /(\b\w+\b|[^\w]+)/g;
    let match: RegExpExecArray | null;
    let idx = 0;
    while ((match = regex.exec(line)) !== null) {
      const token = match[1];
      const color = KEYWORD_COLORS[token];
      if (color) {
        parts.push(React.createElement('span', { key: `${i}-${idx}`, style: { color } }, token));
      } else if (token.startsWith('"') || token.startsWith("'")) {
        parts.push(React.createElement('span', { key: `${i}-${idx}`, style: { color: '#a5d6ff' } }, token));
      } else if (/^\d+$/.test(token)) {
        parts.push(React.createElement('span', { key: `${i}-${idx}`, style: { color: '#79c0ff' } }, token));
      } else {
        parts.push(token);
      }
      idx++;
    }
    return React.createElement('div', { key: i, className: 'code-tribunal__code-line' }, ...parts);
  });
}

// ---------------------------------------------------------------------------
// Diff logic — simple line-by-line LCS diff
// ---------------------------------------------------------------------------

function computeDiff(a: string, b: string): DiffLine[] {
  const linesA = a.split('\n');
  const linesB = b.split('\n');
  const result: DiffLine[] = [];
  const maxLen = Math.max(linesA.length, linesB.length);

  // Simple sequential diff — matches identical lines, marks differences
  let ai = 0;
  let bi = 0;
  while (ai < linesA.length || bi < linesB.length) {
    if (ai < linesA.length && bi < linesB.length && linesA[ai] === linesB[bi]) {
      result.push({ type: 'same', text: linesA[ai] });
      ai++;
      bi++;
    } else if (ai < linesA.length && bi < linesB.length) {
      result.push({ type: 'removed', text: linesA[ai] });
      result.push({ type: 'added', text: linesB[bi] });
      ai++;
      bi++;
    } else if (ai < linesA.length) {
      result.push({ type: 'removed', text: linesA[ai] });
      ai++;
    } else if (bi < linesB.length) {
      result.push({ type: 'added', text: linesB[bi] });
      bi++;
    }
    // Safety: avoid infinite loops on degenerate input
    if (result.length > maxLen * 3) break;
  }
  return result;
}

// ---------------------------------------------------------------------------
// API helpers
// ---------------------------------------------------------------------------

function buildPrompt(description: string, language: Language, mode: Mode): string {
  const lang = LANGUAGE_NAMES[language];
  switch (mode) {
    case 'generate':
      return `Generate ${lang} code for the following requirement:\n\n${description}`;
    case 'review':
      return `Review the following ${lang} code and provide feedback with improved version:\n\n${description}`;
    case 'refactor':
      return `Refactor the following ${lang} code for clarity, performance, and best practices:\n\n${description}`;
  }
}

interface OllamaResponse {
  message?: { content?: string };
}

interface CodestralChoice {
  message?: { content?: string };
}

interface CodestralResponse {
  choices?: CodestralChoice[];
}

async function queryOllama(prompt: string): Promise<ModelResult> {
  const start = performance.now();
  try {
    const res = await fetch('/proxy/ollama/api/chat', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        model: 'qwen2.5-coder',
        messages: [
          { role: 'system', content: SYSTEM_PROMPT },
          { role: 'user', content: prompt },
        ],
        stream: false,
      }),
    });
    if (!res.ok) {
      const text = await res.text().catch(() => '');
      return {
        model: 'Ollama (qwen2.5-coder)',
        code: '',
        latencyMs: performance.now() - start,
        error: `HTTP ${res.status}: ${text || 'Model may not be loaded. Run: ollama pull qwen2.5-coder'}`,
      };
    }
    const data: OllamaResponse = await res.json();
    return {
      model: 'Ollama (qwen2.5-coder)',
      code: extractCodeBlock(data.message?.content ?? ''),
      latencyMs: performance.now() - start,
    };
  } catch (err: unknown) {
    return {
      model: 'Ollama (qwen2.5-coder)',
      code: '',
      latencyMs: performance.now() - start,
      error: `Connection failed — is Ollama running? ${err instanceof Error ? err.message : String(err)}`,
    };
  }
}

async function queryCodestral(prompt: string): Promise<ModelResult> {
  const start = performance.now();
  try {
    const res = await fetch('/proxy/codestral/v1/chat/completions', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        model: 'codestral-latest',
        messages: [
          { role: 'system', content: SYSTEM_PROMPT },
          { role: 'user', content: prompt },
        ],
      }),
    });
    if (!res.ok) {
      const text = await res.text().catch(() => '');
      return {
        model: 'Codestral',
        code: '',
        latencyMs: performance.now() - start,
        error: `HTTP ${res.status}: ${text || 'Codestral proxy unavailable'}`,
      };
    }
    const data: CodestralResponse = await res.json();
    const content = data.choices?.[0]?.message?.content ?? '';
    return {
      model: 'Codestral',
      code: extractCodeBlock(content),
      latencyMs: performance.now() - start,
    };
  } catch (err: unknown) {
    return {
      model: 'Codestral',
      code: '',
      latencyMs: performance.now() - start,
      error: `Connection failed: ${err instanceof Error ? err.message : String(err)}`,
    };
  }
}

/** Strip markdown fences from LLM output */
function extractCodeBlock(raw: string): string {
  const fenceMatch = raw.match(/```[\w]*\n([\s\S]*?)```/);
  return (fenceMatch ? fenceMatch[1] : raw).trim();
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export const CodeTribunal: React.FC = () => {
  const [description, setDescription] = useState('');
  const [language, setLanguage] = useState<Language>('csharp');
  const [mode, setMode] = useState<Mode>('generate');
  const [loading, setLoading] = useState(false);
  const [results, setResults] = useState<ModelResult[]>([]);
  const [showDiff, setShowDiff] = useState(false);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const handleSubmit = useCallback(async () => {
    if (!description.trim() || loading) return;
    setLoading(true);
    setResults([]);
    setShowDiff(false);

    const prompt = buildPrompt(description, language, mode);

    const settled = await Promise.allSettled([
      queryOllama(prompt),
      queryCodestral(prompt),
    ]);

    const newResults: ModelResult[] = settled.map((s) =>
      s.status === 'fulfilled'
        ? s.value
        : { model: 'Unknown', code: '', latencyMs: 0, error: 'Request failed' },
    );

    setResults(newResults);
    setLoading(false);
  }, [description, language, mode, loading]);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
        e.preventDefault();
        handleSubmit();
      }
    },
    [handleSubmit],
  );

  const diffLines = showDiff && results.length >= 2 && results[0].code && results[1].code
    ? computeDiff(results[0].code, results[1].code)
    : null;

  return React.createElement(
    'div',
    { className: 'code-tribunal' },

    // ── Header ──
    React.createElement('div', { className: 'code-tribunal__header' },
      React.createElement('span', { className: 'code-tribunal__title' }, 'Code Tribunal'),
      React.createElement('span', { className: 'code-tribunal__subtitle' }, 'Multi-model code generation'),
    ),

    // ── Input Section ──
    React.createElement('div', { className: 'code-tribunal__input-section' },
      React.createElement('textarea', {
        ref: textareaRef,
        className: 'code-tribunal__textarea',
        placeholder: mode === 'generate'
          ? 'Describe the code to generate...'
          : mode === 'review'
            ? 'Paste code to review...'
            : 'Paste code to refactor...',
        value: description,
        onChange: (e: React.ChangeEvent<HTMLTextAreaElement>) => setDescription(e.target.value),
        onKeyDown: handleKeyDown,
        rows: 4,
      }),

      React.createElement('div', { className: 'code-tribunal__controls' },
        // Language selector
        React.createElement('select', {
          className: 'code-tribunal__select',
          value: language,
          onChange: (e: React.ChangeEvent<HTMLSelectElement>) => setLanguage(e.target.value as Language),
        },
          ...LANGUAGE_OPTIONS.map((opt) =>
            React.createElement('option', { key: opt.value, value: opt.value }, opt.label),
          ),
        ),

        // Mode selector
        React.createElement('select', {
          className: 'code-tribunal__select',
          value: mode,
          onChange: (e: React.ChangeEvent<HTMLSelectElement>) => setMode(e.target.value as Mode),
        },
          ...MODE_OPTIONS.map((opt) =>
            React.createElement('option', { key: opt.value, value: opt.value }, opt.label),
          ),
        ),

        // Submit
        React.createElement('button', {
          className: 'code-tribunal__submit',
          onClick: handleSubmit,
          disabled: loading || !description.trim(),
        }, loading ? 'Querying...' : 'Run'),
      ),

      React.createElement('div', { className: 'code-tribunal__hint' }, 'Ctrl+Enter to submit'),
    ),

    // ── Loading indicator ──
    loading && React.createElement('div', { className: 'code-tribunal__loading' },
      React.createElement('div', { className: 'code-tribunal__spinner' }),
      React.createElement('span', null, 'Querying models in parallel...'),
    ),

    // ── Results Section ──
    results.length > 0 && React.createElement('div', { className: 'code-tribunal__results' },
      ...results.map((r, i) =>
        React.createElement('div', { key: i, className: 'code-tribunal__card' },
          React.createElement('div', { className: 'code-tribunal__card-header' },
            React.createElement('span', { className: 'code-tribunal__model-name' }, r.model),
            React.createElement('span', { className: 'code-tribunal__latency' }, `${Math.round(r.latencyMs)}ms`),
          ),
          r.error
            ? React.createElement('div', { className: 'code-tribunal__error' }, r.error)
            : React.createElement('pre', { className: 'code-tribunal__code-block' },
                React.createElement('code', null, ...highlightCode(r.code)),
              ),
        ),
      ),
    ),

    // ── Diff toggle ──
    results.length >= 2 && results[0].code && results[1].code &&
      React.createElement('button', {
        className: 'code-tribunal__diff-toggle',
        onClick: () => setShowDiff((v) => !v),
      }, showDiff ? 'Hide Diff' : 'Show Diff'),

    // ── Diff Section ──
    diffLines && React.createElement('div', { className: 'code-tribunal__diff' },
      React.createElement('div', { className: 'code-tribunal__diff-header' },
        React.createElement('span', null, `${results[0].model} vs ${results[1].model}`),
      ),
      React.createElement('pre', { className: 'code-tribunal__diff-block' },
        ...diffLines.map((line, i) =>
          React.createElement('div', {
            key: i,
            className: `code-tribunal__diff-line code-tribunal__diff-line--${line.type}`,
          },
            line.type === 'added' ? '+ ' : line.type === 'removed' ? '- ' : '  ',
            line.text,
          ),
        ),
      ),
    ),
  );
};
