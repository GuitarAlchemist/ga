---
title: "feat: WebMCP prep — declarative-API annotations on demos.guitaralchemist.com/chatbot/"
type: feat
status: draft-v0.1
date: 2026-05-16
owner: GA frontend
related:
  - Apps/GaChatbot.Api/wwwroot/index.html
  - docs/runbooks/chatbot-deploy.md
  - docs/plans/2026-05-07-chatbot-roadmap.md
reversibility: |
  Phase 0 declarative attributes = two-way (HTML attributes; ignored by non-WebMCP browsers).
  Phase 1+ imperative API (navigator.modelContext.registerTool) = two-way at the JS level.
  Phase 2 schema-evolution tracking = one-way at the spec-version level once W3C REC freezes.
revisit_trigger: |
  W3C Community Group Draft promotion to Recommendation; OR Chrome 146 ships GA; OR a competitor MCP-style web standard (none today, but watching).
---

# WebMCP prep

## Problem frame

[WebMCP](https://searchengineland.com/webmcp-prepare-now-477548) is a W3C Community Group Draft (Chrome 146 beta preview) co-authored by Google + Microsoft, with Cloudflare integration underway. It exposes website actions to AI agents through structured machine-readable attributes — eliminating DOM scraping for the agents that will increasingly visit web surfaces autonomously.

GA already has a deep MCP investment via the `ga-dsl` plugin (50+ stdio MCP tools for Claude Code / Antigravity / Augment). WebMCP is the **browser-side complement**: when an AI agent loads `demos.guitaralchemist.com/chatbot/` in a WebMCP-aware browser, it should be able to invoke the chat action structurally rather than parsing the DOM.

Today's surface: `Apps/GaChatbot.Api/wwwroot/index.html` is a single-page static-rendered chat UI with one form (`#chatForm`), one textarea (`#messageInput` mapping to `ChatRequest.Message`), one submit button. The form POSTs to `/api/chatbot/chat` per the runbook.

## Why prepare now

| Factor | Today | 12 months |
|---|---|---|
| WebMCP-capable browsers | Chrome 146 beta only | All major Chromium |
| Agent traffic from browsers | ~0% of GA traffic | unknown but rising |
| Cost to annotate | ~5 lines of HTML attributes | same |
| Cost to retrofit later | ~5 lines of HTML attributes + audit of every wwwroot page | same per page |
| Spec churn risk | high (W3C CG Draft) | low (presumed REC) |

The argument is "schema.org-style early-mover signal": cheap, additive, well-known cost, **non-zero upside if browser-native agents start preferring annotated sites**. The risk if the spec changes is a 5-line refactor.

## Decisions

| # | Decision | Default | Rationale |
|---|---|---|---|
| **D-api-tier** | Declarative or imperative API? | **Declarative for v0.1** | Smallest possible surface; HTML-only; no JS changes. Imperative API (`navigator.modelContext.registerTool()`) is Phase 1+ when we need multi-step flows (e.g., "show me a chord" → render → "now transpose it"). |
| **D-tool-name** | What's the canonical tool name on the chat form? | **`ask-guitar-alchemist`** | Hyphen-separated lowercase per WebMCP convention; matches the existing CLAUDE.md user-facing alias. |
| **D-autosubmit** | Should the form auto-submit when the agent fills it? | **`false`** | Conversational UX — operator/agent should review the prepared question before sending. Auto-submit is appropriate for one-shot transactional forms; this is a chat. |
| **D-param-naming** | Match `ChatRequest.Message` C# field or use snake_case? | **`Message`** | Matches the actual HTTP contract (see `Apps/GaChatbot.Api/Controllers/ChatbotController.cs` line 264). Diverging would just require translation. |
| **D-deployment** | When does the annotation hit demos.guitaralchemist.com? | **Next chatbot-api redeploy** | Follow `docs/runbooks/chatbot-deploy.md`. No cache busting required; HTML attributes are inert until a WebMCP-aware browser parses them. |

## Phases

### Phase 0 — Declarative API annotations (this PR)

- `Apps/GaChatbot.Api/wwwroot/index.html`: add `toolname`, `tooldescription`, `toolautosubmit` to the form; `toolparamdescription` + `name="Message"` to the textarea.
- Document the decision via this plan doc.
- BACKLOG entry tracking future phases.

Verification: spec-conformance check is currently only available in Chrome 146 beta. Pre-merge: visual diff of `index.html` shows attributes added; no behavioral change for normal browsers.

### Phase 1 — Imperative API for multi-step flows (deferred)

- Register tools via `navigator.modelContext.registerTool()` for actions that can't map to a single form submission. Candidates: `play-chord-on-fretboard`, `transpose-chord-progression`, `render-vextab`.
- Out of scope for v0.1; revisit when WebMCP spec stabilizes + at least one competitor site (other than Cloudflare) ships imperative tools.

### Phase 2 — Schema evolution tracking (deferred)

- Pin the WebMCP draft version in a comment near the annotations.
- When W3C promotes the draft to REC (Recommendation), audit and re-pin.

## Success criteria

1. **Phase 0**: `index.html` validates against the WebMCP declarative-API attribute set (manual check via Chrome 146 beta DevTools). No regression on non-WebMCP browsers.
2. **Phase 0**: at least one WebMCP-aware client (e.g. Claude in browser-aware mode, when that lands) can discover and invoke the chat action without DOM scraping.
3. **Phase 1**: imperative API tools registered for ≥2 chat actions that can't be expressed as forms.

## One-way doors

| # | Door | When | Revisit trigger |
|---|---|---|---|
| **OWD-1** | None in Phase 0 | — | Pure additive HTML. |
| **OWD-2** | Imperative API tool names | Phase 1 | If we ship `play-chord` and agents start using it, renaming is breaking. Treat tool names like API endpoint paths. |
| **OWD-3** | Spec-version pin | Phase 2 | If we pin to draft v0.1 and W3C REC drops attributes, we'd need a Phase 0.5 refactor. Mitigate by tracking the spec until REC. |

## Out of scope

- Imperative API wiring (Phase 1+)
- Annotations on non-chatbot pages (the rest of demos.guitaralchemist.com)
- WebMCP spec-conformance tests in CI (Chrome 146 isn't on CI runners)
- Cross-browser fallbacks (declarative attrs are universal-safe)
- Server-side discoverability hints (`.well-known/mcp-tools`, etc. — not part of WebMCP draft)

## Related

- Article that motivated this: [WebMCP: Why now is the time to prepare](https://searchengineland.com/webmcp-prepare-now-477548) — Search Engine Land, 2026
- W3C Community Group Draft (cite once spec lands): TBD
- Chrome 146 beta release notes (cite once GA): TBD
- `docs/contracts/2026-05-16-overseer-halt-marker.contract.md` — also a "lightweight contract" style; this plan follows the same shape
- `docs/runbooks/chatbot-deploy.md` — operator path to ship the change to demos
