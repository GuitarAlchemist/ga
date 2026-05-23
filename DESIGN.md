---
name: Guitar Alchemist
colors:
  primary: "#1d4ed8"
  primary-soft: "#1f6feb"
  secondary: "#06b6d4"
  success: "#168a4a"
  warning: "#b56a00"
  error: "#c2410c"
  neutral: "#f6f7f9"
  surface: "#ffffff"
  surface-alt: "#f1f5f9"
  text-primary: "#172033"
  text-secondary: "#6c7278"
  text-disabled: "#9ca3af"
  border: "#d7dce5"
  divider: "#e0e0e0"
typography:
  fontFamily: "Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif"
  fontFamily-mono: "ui-monospace, SFMono-Regular, 'Cascadia Code', Consolas, monospace"
  h1:
    fontFamily: Inter
    fontSize: "28px"
    fontWeight: 700
    lineHeight: 1.2
  h2:
    fontFamily: Inter
    fontSize: "1.5rem"
    fontWeight: 700
    lineHeight: 1.25
  h3:
    fontFamily: Inter
    fontSize: "1.25rem"
    fontWeight: 600
    lineHeight: 1.3
  h6:
    fontFamily: Inter
    fontSize: "1rem"
    fontWeight: 600
    lineHeight: 1.4
  body:
    fontFamily: Inter
    fontSize: "1rem"
    fontWeight: 400
    lineHeight: 1.5
  body-sm:
    fontFamily: Inter
    fontSize: "0.875rem"
    fontWeight: 400
    lineHeight: 1.5
  caption:
    fontFamily: Inter
    fontSize: "0.75rem"
    fontWeight: 400
    lineHeight: 1.4
  code:
    fontFamily: mono
    fontSize: "0.85em"
spacing:
  xs: "4px"
  sm: "8px"
  md: "16px"
  lg: "24px"
  xl: "32px"
  xxl: "48px"
rounded:
  none: "0"
  sm: "4px"
  md: "6px"
  lg: "8px"
  xl: "12px"
  pill: "999px"
elevation:
  card: "0 1px 3px rgba(23, 32, 51, 0.06), 0 1px 2px rgba(23, 32, 51, 0.04)"
  hero: "0 8px 24px rgba(29, 78, 216, 0.18)"
---

# Guitar Alchemist — Visual Identity

This file is the **agent-readable design source of truth** for GA's UI surfaces. It follows the [google-labs-code/design.md](https://github.com/google-labs-code/design.md) spec: machine-readable tokens in the YAML frontmatter, human rationale below. Adoption plan: [`docs/plans/2026-05-23-arch-design-md-adoption-plan.md`](docs/plans/2026-05-23-arch-design-md-adoption-plan.md).

**Status (2026-05-23):** Phase 1 — documentation only. Tokens describe the existing dashboard; nothing is generated from this file yet. Phase 2 (planned) will emit `ReactComponents/ga-react-components/src/theme.ts` from this file via a generator + pre-commit sync hook.

## Overview

Two design moods coexist:

- **Premium-technical** (the dashboard at `/test` and the demos showcase): tight Inter typography, deep cobalt-blue hero gradient, generous whitespace, success-green and warning-amber accents derived from earthy paint pigments rather than neon.
- **Conversational-grounded** (the chatbot at `/chatbot/`): same primary blue but a much richer micro-palette for response-quality badges (verified/warn/error/info), grounding-source chips, and confidence indicators. Treat the chatbot's surface-level palette as derivative of this file — adding new chatbot chip colors should reach for the tokens here first.

## Colors

The palette is deep-cobalt led, with cyan as a complementary "in motion" hue. All neutrals are warm (slight gray-blue tint). No pure white background; no jet black text.

- **primary (#1d4ed8) — Cobalt:** the chatbot launch button, the hero gradient start, primary actions across the dashboard. Calm and authoritative.
- **primary-soft (#1f6feb) — Cobalt Light:** the hero gradient middle stop. Adds depth without changing hue.
- **secondary (#06b6d4) — Cyan:** the hero gradient end, motion accents (the commit-activity bar chart uses MUI default `#1976d2`, which sits between primary and secondary). Use this hue when something is "live" or "streaming".
- **success (#168a4a) — Forest Green:** QA-verified badges, "OK" pills. Not bright lime — desaturated, paint-pigment feel.
- **warning (#b56a00) — Burnt Amber:** soft-budget overruns, slow responses, "regression detected" headers, Operational TODO icon. Reads as caution without alarm.
- **error (#c2410c) — Brick Red:** reserved for hard failures. Currently used only by the chatbot for HTTP 5xx fall-through. Status DOTS use this too. Avoid for "regression" — that's warning amber.
- **neutral (#f6f7f9) — Page Background:** the off-white the dashboard and chatbot pages sit on. Warmer than `#fff`.
- **surface (#ffffff) — Card:** the white inside Paper components. The only place pure white appears.
- **surface-alt (#f1f5f9) — Hover / Code Block:** background for hover states, inline `<code>` blocks, and the manifest-banner code chip.
- **text-primary (#172033) — Ink:** body text on `surface`. Passes WCAG AAA (contrast 14.4:1).
- **text-secondary (#6c7278) — Slate:** secondary labels, captions, mtime/relative-time strings.
- **text-disabled (#9ca3af) — Mist:** placeholder values (the "—" in the AI Contributors "Tokens left" column).
- **border (#d7dce5):** card borders in the chatbot. Lighter than divider.
- **divider (#e0e0e0):** row separators inside cards.

## Typography

Single typeface family — **Inter** — with the system UI stack as fallback. Code uses the OS-native mono stack so it stays sharp at small sizes.

Scale follows the dashboard's actual usage:

- `h1` (28px / 700): "GA Chatbot" hero title, "Dev Manifest" page title.
- `h2` (1.5rem / 700): page section headers in `ManifestViewer`.
- `h3` (1.25rem / 600): showcase modal category headers.
- `h6` (1rem / 600): card titles ("Epic Progress", "Commit Activity", "AI Contributors").
- `body` (1rem): default running text.
- `body-sm` (0.875rem): card body content; primary on dense tables.
- `caption` (0.75rem): descriptions, metadata, code labels.
- `code` (0.85em mono): inline `<code>`, the curl-command box, SHA hashes.

## Spacing

8px base, doubled — matches MUI's `theme.spacing(1) = 8px` so the dashboard's `sx={{ p: 2 }}` (= 16px) maps to `md`.

| token | px | use |
|---|---|---|
| xs | 4 | gap inside chip rows |
| sm | 8 | inline icon-to-text |
| md | 16 | card padding, paragraph margin |
| lg | 24 | section margin between stacks |
| xl | 32 | tab-content top padding |
| xxl | 48 | hero block vertical breathing room |

## Rounded

Subtle. No fully sharp corners, no extreme rounding except for pills.

- `sm` (4px): inline code blocks, copyable command boxes.
- `md` (6px): default Paper / Card border-radius (MUI's `shape.borderRadius`).
- `lg` (8px): chatbot message bubbles, modal corners.
- `xl` (12px): the chatbot hero block.
- `pill` (999px): chips, badges, status pills.

## Elevation

Two shadow tokens. Most cards use `card`. Only the chatbot hero uses `hero`.

- `card`: barely-there shadow that lifts a Paper off the page background.
- `hero`: a tinted blue shadow under the chatbot hero block; uses primary color at 18% alpha so the "lift" feels related to the gradient above.

## What this file deliberately does NOT cover

- **Three.js / R3F demo colors:** scene-specific (sunflower yellow, sand-dune ochre, ocean blue gradients, fluffy-grass green). Out of scope — each demo is its own visual essay.
- **VexFlow / music notation:** locked to VexFlow's defaults (black on white) for sheet-music clarity.
- **Dark mode:** not yet defined. When added, every token here gets a dark counterpart; the structure stays.
- **Animation / motion:** no timing or easing tokens yet. Transitions today use MUI's defaults.

## Revisit

This file is canonical for Phase 1 (documentation). The next change to it should be Phase 2 of the [adoption plan](docs/plans/2026-05-23-arch-design-md-adoption-plan.md): a generator at `Scripts/gen-theme-from-design.ts` that emits `ReactComponents/ga-react-components/src/theme.ts`. Until that lands, agents reading this file produce mockups matching these tokens but the existing dashboard's hard-coded hex values keep working.
