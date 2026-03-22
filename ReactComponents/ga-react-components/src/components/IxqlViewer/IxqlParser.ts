// IxQL Parser — Parse IxQL text into a graph of bindings and edges

import type { IxqlBinding, IxqlEdge, IxqlGraph, NodeKind } from './types';

/** Detect what kind of node an expression represents */
function classifyExpression(expr: string): NodeKind {
  const trimmed = expr.trim();
  if (trimmed.startsWith('fan_out(')) return 'fan_out';
  if (trimmed.startsWith('parallel(')) return 'parallel';
  if (trimmed.startsWith('when ') || trimmed.startsWith('when(')) return 'when';
  if (trimmed.startsWith('filter(')) return 'filter';
  if (trimmed.startsWith('head(')) return 'head';
  if (/\bwrite\s*\(/.test(trimmed)) return 'write';
  if (/\balert\s*\(/.test(trimmed)) return 'alert';
  if (trimmed.startsWith('governance_gate(')) return 'governance_gate';
  if (trimmed.includes('compound:')) return 'compound';
  return 'binding';
}

/** Detect execution mode from expression content */
function detectExecutionMode(expr: string): 'serial' | 'parallel' {
  if (/\bfan_out\s*\(/.test(expr)) return 'parallel';
  if (/\bparallel\s*\(/.test(expr)) return 'parallel';
  return 'serial';
}

/** Extract binding names referenced in an expression */
function extractReferences(expr: string, knownBindings: string[]): string[] {
  const refs: string[] = [];
  for (const name of knownBindings) {
    // Match whole-word occurrences of binding names in the expression
    const regex = new RegExp(`\\b${escapeRegex(name)}\\b`);
    if (regex.test(expr)) {
      refs.push(name);
    }
  }
  return refs;
}

function escapeRegex(s: string): string {
  return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/** Estimate relative cost of a binding (heuristic based on expression complexity) */
function estimateCost(expr: string): number {
  let cost = 1;
  // Nested fan_out/parallel add cost
  const fanOutCount = (expr.match(/fan_out\s*\(/g) || []).length;
  const parallelCount = (expr.match(/parallel\s*\(/g) || []).length;
  cost += fanOutCount * 2 + parallelCount * 2;
  // Arrow chains add cost
  const arrowCount = (expr.match(/→/g) || []).length;
  cost += arrowCount * 0.5;
  // Filter/transform steps
  const filterCount = (expr.match(/filter\s*\(/g) || []).length;
  cost += filterCount * 0.3;
  return cost;
}

/**
 * Collect the full expression for a binding, including multi-line continuations.
 * A continuation line is one that starts with whitespace or starts with → or
 * is inside a parenthesized block.
 */
function collectExpression(lines: string[], startIdx: number): { expr: string; endIdx: number } {
  let expr = lines[startIdx];
  let parenDepth = countParens(expr);
  let i = startIdx + 1;

  while (i < lines.length) {
    const line = lines[i];
    const trimmed = line.trim();

    // Skip empty lines and comments within expression blocks
    if (trimmed === '' && parenDepth > 0) {
      expr += '\n' + line;
      i++;
      continue;
    }

    // Continue if we're inside unclosed parens
    if (parenDepth > 0) {
      expr += '\n' + line;
      parenDepth += countParens(line);
      i++;
      continue;
    }

    // Continue if line starts with → (pipeline chain)
    if (trimmed.startsWith('→')) {
      expr += '\n' + line;
      parenDepth += countParens(line);
      i++;
      continue;
    }

    // Continue if line is indented continuation (not a new binding or comment)
    if (line.match(/^\s{2,}/) && !trimmed.startsWith('--') && !trimmed.match(/^\w[\w_]*\s*<-/)) {
      expr += '\n' + line;
      parenDepth += countParens(line);
      i++;
      continue;
    }

    break;
  }

  return { expr, endIdx: i - 1 };
}

function countParens(line: string): number {
  let depth = 0;
  // Ignore parens inside strings
  let inString = false;
  let stringChar = '';
  for (const ch of line) {
    if (inString) {
      if (ch === stringChar) inString = false;
      continue;
    }
    if (ch === '"' || ch === "'") {
      inString = true;
      stringChar = ch;
      continue;
    }
    if (ch === '(') depth++;
    if (ch === ')') depth--;
  }
  return depth;
}

/** Parse an IxQL source string into a graph */
export function parseIxql(source: string): IxqlGraph {
  const lines = source.split('\n');
  const bindings: IxqlBinding[] = [];
  const bindingNames: string[] = [];
  let currentMarkdownComment: string | undefined;
  let currentPlainComments: string[] = [];
  let syntheticId = 0;

  let i = 0;
  while (i < lines.length) {
    const line = lines[i];
    const trimmed = line.trim();

    // Skip empty lines
    if (trimmed === '') {
      // Reset plain comments on blank line (they don't attach across gaps)
      if (currentPlainComments.length > 0 && !currentMarkdownComment) {
        currentPlainComments = [];
      }
      i++;
      continue;
    }

    // Markdown comment block (--- delimited)
    if (trimmed === '---' || trimmed.startsWith('---')) {
      // Check if this is a block delimiter
      if (trimmed === '---') {
        // Find closing ---
        const mdLines: string[] = [];
        i++;
        while (i < lines.length && lines[i].trim() !== '---') {
          mdLines.push(lines[i]);
          i++;
        }
        currentMarkdownComment = mdLines.join('\n');
        i++; // skip closing ---
        continue;
      } else {
        // Single line markdown comment: --- content
        currentMarkdownComment = trimmed.slice(3).trim();
        i++;
        continue;
      }
    }

    // Plain comment
    if (trimmed.startsWith('--') && !trimmed.startsWith('---')) {
      currentPlainComments.push(trimmed.slice(2).trim());
      i++;
      continue;
    }

    // Binding: name <- expression
    const bindingMatch = trimmed.match(/^(\w[\w_]*)\s*<-\s*(.*)/);
    if (bindingMatch) {
      const name = bindingMatch[1];
      const { expr, endIdx } = collectExpression(lines, i);
      const fullExpr = expr.replace(/^\w[\w_]*\s*<-\s*/, '').trim();

      const kind = classifyExpression(fullExpr);
      const mode = detectExecutionMode(fullExpr);

      bindings.push({
        id: name,
        name,
        expression: fullExpr,
        kind: kind === 'binding' ? detectTopLevelKind(fullExpr) : kind,
        line: i + 1,
        references: [],
        referencedBy: [],
        executionMode: mode,
        markdownComment: currentMarkdownComment,
        plainComments: [...currentPlainComments],
        lolliStatus: 'live',
        estimatedCost: estimateCost(fullExpr),
      });
      bindingNames.push(name);

      currentMarkdownComment = undefined;
      currentPlainComments = [];
      i = endIdx + 1;
      continue;
    }

    // Standalone statements: governance_gate, parallel, when, write, alert, compound
    const standaloneKinds: Array<{ regex: RegExp; kind: NodeKind }> = [
      { regex: /^governance_gate\s*\(/, kind: 'governance_gate' },
      { regex: /^parallel\s*\(/, kind: 'parallel' },
      { regex: /^when\s+/, kind: 'when' },
      { regex: /^write\s*\(/, kind: 'write' },
      { regex: /^alert\s*\(/, kind: 'alert' },
      { regex: /^→\s*compound:/, kind: 'compound' },
      { regex: /^compound:/, kind: 'compound' },
    ];

    let matched = false;
    for (const { regex, kind } of standaloneKinds) {
      if (regex.test(trimmed)) {
        const { expr, endIdx } = collectExpression(lines, i);
        const id = `__${kind}_${syntheticId++}`;

        bindings.push({
          id,
          name: kind === 'compound' ? 'compound' : `${kind}`,
          expression: expr.trim(),
          kind,
          line: i + 1,
          references: [],
          referencedBy: [],
          executionMode: kind === 'parallel' || kind === 'fan_out' ? 'parallel' : 'serial',
          markdownComment: currentMarkdownComment,
          plainComments: [...currentPlainComments],
          lolliStatus: 'live',
          estimatedCost: estimateCost(expr),
        });
        bindingNames.push(id);

        currentMarkdownComment = undefined;
        currentPlainComments = [];
        i = endIdx + 1;
        matched = true;
        break;
      }
    }

    if (matched) continue;

    // Pipeline continuation attached to previous binding (→ chain not starting a line in an expression)
    if (trimmed.startsWith('→') && bindings.length > 0) {
      const last = bindings[bindings.length - 1];
      const { expr, endIdx } = collectExpression(lines, i);
      last.expression += '\n' + expr.trim();
      last.estimatedCost = estimateCost(last.expression);
      i = endIdx + 1;
      continue;
    }

    i++;
  }

  // Second pass: resolve references
  for (const binding of bindings) {
    const otherNames = bindingNames.filter((n) => n !== binding.id);
    binding.references = extractReferences(binding.expression, otherNames);
  }

  // Build referencedBy
  for (const binding of bindings) {
    for (const ref of binding.references) {
      const target = bindings.find((b) => b.id === ref);
      if (target && !target.referencedBy.includes(binding.id)) {
        target.referencedBy.push(binding.id);
      }
    }
  }

  // Build edges from references
  const edges: IxqlEdge[] = [];
  for (const binding of bindings) {
    for (const ref of binding.references) {
      edges.push({
        id: `${ref}->${binding.id}`,
        source: ref,
        target: binding.id,
      });
    }
  }

  // Also build sequential edges for bindings that don't reference others
  // but appear sequentially (implicit data flow)
  for (let j = 1; j < bindings.length; j++) {
    const prev = bindings[j - 1];
    const curr = bindings[j];
    // If current has no explicit references and previous is not compound/gate
    if (
      curr.references.length === 0 &&
      prev.kind !== 'compound' &&
      curr.kind !== 'compound' &&
      !edges.some((e) => e.target === curr.id)
    ) {
      edges.push({
        id: `${prev.id}->${curr.id}`,
        source: prev.id,
        target: curr.id,
        label: 'sequential',
      });
    }
  }

  return { bindings, edges, rawSource: source };
}

/** Further classify a generic binding by looking at its expression */
function detectTopLevelKind(expr: string): NodeKind {
  if (/\bfan_out\s*\(/.test(expr)) return 'fan_out';
  if (/\bparallel\s*\(/.test(expr)) return 'parallel';
  if (/\bgovernance_gate\s*\(/.test(expr)) return 'governance_gate';
  if (/\bcompound:/.test(expr)) return 'compound';
  return 'binding';
}
