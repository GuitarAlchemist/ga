# Session Plan: LLM Usage Panel + Powerful Notebooks

**Created:** 2026-03-30
**Mode:** Parallel agent teams (isolated worktrees)

## What You'll End Up With

Two new Prime Radiant capabilities shipping simultaneously:
1. **LLM Usage Panel** — Token tracking, quota display, cost estimates, algedonic alerts on limits
2. **Powerful Notebooks** — CREATE FORM with hexavalent validation, reactive cells, cross-cell signal linking

## Workstream A: LLM Usage Panel

**Agent:** `llm-usage-panel`
**Isolation:** worktree (independent branch)

### Files to modify:
- `LLMStatus.tsx` — Add usage metrics display (tokens, cost, quota)
- `MultiModelFanOut.ts` — Capture rate-limit headers from responses
- `ForceRadiant.tsx` — Wire updated LLM panel
- `styles.css` — Usage bar styling

### Implementation:
1. Create `LLMUsageTracker.ts` — session-scoped token counter
   - Track tokens per model per request via response headers
   - Anthropic: `x-ratelimit-limit-tokens`, `x-ratelimit-remaining-tokens`
   - OpenAI: `x-ratelimit-remaining-tokens`, `x-ratelimit-limit-tokens`
   - Mistral: `x-ratelimit-remaining-tokens`
   - Accumulate session totals, compute cost estimates
2. Extend `LLMStatus.tsx` — Add usage section
   - Token usage bar per model (used/remaining)
   - Session cost estimate (price per 1K tokens)
   - Algedonic alert when remaining < 10%
3. Wire algedonic signals for quota warnings

### Cost model (approximate):
- Claude Opus: $15/1M input, $75/1M output
- Claude Sonnet: $3/1M input, $15/1M output
- GPT-4o: $2.50/1M input, $10/1M output
- Codestral: $0.30/1M input, $0.90/1M output

## Workstream B: Powerful Notebooks

**Agent:** `powerful-notebooks`
**Isolation:** worktree (independent branch)

### Files to modify:
- `IxqlControlParser.ts` — Add CREATE FORM command type
- `IxqlWidgetSpec.ts` — Add FormSpec type, compileForm()
- `LiveNotebook.tsx` — Reactive cells, cross-cell signals, form cells
- `ForceRadiant.tsx` — Wire CREATE FORM dispatch

### Implementation:
1. Parser: `CreateFormCommand` type
   - `CREATE FORM "id" KIND mui-form FIELDS [ truth_value: enum(...), confidence: slider(0,1) ]`
   - `CONSTRAIN`, `REQUIRE ... WHEN`, `HEXAVALENT validation=true`
   - `SUBMIT COMMAND governance.updateBelief`
   - `ON_SUCCESS REFRESH "panel-id"`
2. Compiler: `FormSpec` type with field definitions, constraints, submit action
3. `IxqlFormPanel.tsx` — MUI-style form rendering
   - Hexavalent enum selector (T/P/U/D/F/C with color coding)
   - Confidence slider (0.0-1.0)
   - Justification text field (required when delta > 0.2)
   - Constitutional validation badge (GOVERNED BY)
4. Notebook reactive cells
   - Cells with SUBSCRIBE auto-re-render on signal change
   - Visual indicator for reactive cells (pulsing border)
5. Cross-cell signal linking demo in sample notebooks

## Phase Weights

```
DISCOVER ████ 10%   — Infrastructure already built
DEFINE   ████ 10%   — Requirements clear from spec
DEVELOP  ████████████████ 50%   — Main implementation work
DELIVER  ██████████████ 30%   — Testing + wiring + CSS
```

## Provider Availability
- Codex CLI: Available
- Gemini CLI: Available
- Ollama: Available
- Claude: Available

## Execution Commands

Parallel worktree agents:
```
Agent A: llm-usage-panel (worktree isolation)
Agent B: powerful-notebooks (worktree isolation)
```

## Success Criteria
- LLM panel shows token usage per model with quota bars
- Notebooks support CREATE FORM with hexavalent validation
- Cross-cell PUBLISH/SUBSCRIBE works in notebooks
- Reactive cells auto-update on signal changes
- Both merge cleanly into main
