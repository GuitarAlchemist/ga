---
title: Adopt google-labs-code/design.md as the design-system source of truth
created: 2026-05-23
owner: spareilleux
status: draft
revisit_trigger: when next non-trivial dashboard UI change lands (next /test/* page or theme tweak) OR if Google Labs cuts a 1.0 of the spec
reversibility: two-way door — DESIGN.md is purely additive until Phase 2 wires the generator. Phase 1 alone is free to abandon.
---

# Adopt google-labs-code/design.md as the design-system source of truth

## Problem

GA has at least four UI surfaces with no shared design vocabulary:

1. **Dashboard** (`ReactComponents/ga-react-components/`) — MUI v5 with `sx` props, no central theme.
2. **Chatbot** (`Apps/GaChatbot.Api/wwwroot/index.html`) — inline `<style>` block, hard-coded `#1d4ed8` / `#06b6d4` / `#168a4a`.
3. **Legacy ga-client** (`Apps/ga-client/`) — its own MUI + R3F styling.
4. **45 demos** under `/test/*` — each one freelances colors, fonts, spacing for its specific viz (Three.js shaders, Pixi, custom canvas).

Three AI agents (Claude Code, Antigravity, Codex) take turns editing this code. None of them share a palette reference. Result: the dashboard hero is `#1d4ed8 → #06b6d4` (blue→cyan gradient); the chatbot launch button is `#1f6feb`; the QA badges are `#168a4a` (green) and `#b56a00` (amber). All chosen independently, none reusable.

[google-labs-code/design.md](https://github.com/google-labs-code/design.md) (14.6k stars, born 2026-04-10) is a spec for an agent-readable `DESIGN.md` file:

- **YAML frontmatter** holds normative tokens (colors, typography, spacing, rounded).
- **Markdown prose** holds rationale ("why Boston Clay for tertiary").
- **`npx @google/design.md lint`** validates token refs + WCAG contrast.
- **`npx @google/design.md diff`** catches regressions across versions.

The agent-readability win is real. The risk is creating a **third** source of truth alongside the (currently nonexistent) MUI theme and the ad-hoc inline styles — which is exactly the kind of drift the spec exists to prevent.

## The shape of the right answer

DESIGN.md only pays off if **the MUI theme and the chatbot CSS are both generated from it** (or at minimum lint-gated by it). Without that, agents will read DESIGN.md, then commit a hex literal anyway, and DESIGN.md becomes a stale rationale file nobody enforces.

## Phase 1 — Author DESIGN.md from the current dashboard palette (low risk, ~1h)

Goal: produce a single `DESIGN.md` at repo root that accurately describes what `/test` already looks like. No code change downstream. The dashboard continues to use its hard-coded values.

Deliverables:

- `DESIGN.md` at `C:\Users\spare\source\repos\ga\DESIGN.md`. YAML frontmatter captures:
  - `colors`: primary (#1d4ed8 dashboard hero start), secondary (#06b6d4 hero end), tertiary (#168a4a QA-verified green), warning (#b56a00 amber), neutral (#f6f7f9 page bg), surface (#ffffff card), text-primary (#172033), text-secondary (#6c7278)
  - `typography`: family (Inter / ui-sans-serif), h1 (28px / 700), h4 (1.5rem / 600), body (1rem / 1.5), caption (0.75rem)
  - `spacing`: xs (4px), sm (8px), md (16px), lg (24px), xl (32px)
  - `rounded`: sm (4px), md (6px), lg (8px), xl (12px)
- Markdown sections explain the why: why warm-blue gradients for the chatbot hero (calm + technical), why green/amber for QA badges (universal verified/slow semantics), why no red (verified-fail surfaces as warning amber + explicit error text, not red, to avoid alarming public visitors).
- `npx @google/design.md lint DESIGN.md` exits 0 (or surfaces fixable findings).

Verification:
- Lint passes.
- WCAG AA contrast confirmed on `text-primary` over `surface` and over `neutral`.
- One AI agent (Claude or Codex) given the file can produce a button mockup using the exact tokens.

Open one-way doors at this stage: none. DESIGN.md is purely documentation.

## Phase 2 — Wire DESIGN.md into the MUI theme via a generator (medium, ~3-4h)

Goal: a build step that reads `DESIGN.md` and emits `ReactComponents/ga-react-components/src/theme.ts` so MUI components use the spec automatically. The chatbot's inline CSS gets a parallel emitter or a `<link>` to a generated stylesheet.

Deliverables:

- `Scripts/gen-theme-from-design.ts` (or `.ps1` + `node`) that parses `DESIGN.md` frontmatter and writes `src/theme.ts`:
  ```ts
  import { createTheme } from '@mui/material/styles';
  // GENERATED from DESIGN.md @ <sha>. Do not edit by hand — edit DESIGN.md and rerun gen-theme.
  export const theme = createTheme({
    palette: { primary: { main: '#1d4ed8' }, secondary: { main: '#06b6d4' }, success: { main: '#168a4a' }, warning: { main: '#b56a00' } },
    typography: { fontFamily: 'Inter, ui-sans-serif, system-ui, sans-serif', h1: { fontSize: 28, fontWeight: 700 } },
    shape: { borderRadius: 6 },
  });
  ```
- `main.tsx` wraps `<App>` in `<ThemeProvider theme={theme}>`.
- Pre-commit hook: run lint + check `theme.ts` matches DESIGN.md (regenerate, diff). If out of sync, fail the commit and tell the dev to rerun the generator. Mirrors the existing AGENTS.md sync pattern.
- Optional Phase 2.5: a parallel emitter for `Apps/GaChatbot.Api/wwwroot/design-tokens.css` so the chatbot's inline CSS becomes `var(--color-primary)` references.

Verification:
- Existing dashboard renders identically (screenshot diff vs baseline) — palette is unchanged because we authored the spec FROM the current colors.
- A token change in DESIGN.md (e.g., shift `tertiary` to a new green) propagates to MUI components on the next `npm run gen:theme`.
- Pre-commit blocks a commit that edits `theme.ts` without editing DESIGN.md.

One-way doors:
- The generator's input format is locked to whatever `@google/design.md` spec version we pin in `package.json`. Schema migrations need a coordinated bump.
- Once components rely on `theme.palette.tertiary.main`, removing the `tertiary` token from DESIGN.md is a breaking change. Treat the token names as a public API.

Reversibility: if Phase 2 turns out wrong, revert the generator + `theme.ts` + the `<ThemeProvider>` wrapper. DESIGN.md stays as documentation. No data loss, no schema migration.

## What this plan deliberately does NOT cover

- **Three.js / R3F demos:** their colors are scene-specific (sunflower yellow, sand-dune ochre, ocean blue) and won't be expressed as 4-token palettes. Out of scope.
- **45-demo `/test/*` pages:** each is a sandbox; not worth normalizing.
- **Public chatbot dark mode / themes:** can come later via DESIGN.md variants.
- **Demerzel governance UI:** different repo, different stakeholders.

## Revisit trigger

Pick this back up when **either**:

1. The next non-trivial dashboard or chatbot UI change lands and one of the agents asks "what color should I use for X?" — Phase 1 becomes the answer.
2. Google Labs ships `@google/design.md` 1.0 (spec freeze announcement). Until then the spec may evolve in breaking ways and the generator in Phase 2 would chase a moving target.
