# Guitar Alchemist — Product Roadmap (PI)

> Program-Increment roadmap for `GuitarAlchemist/ga`. Implements issue **#482**.
> Parent org roadmap: **GuitarAlchemist/.github#5** · AIW roadmap: **GuitarAlchemist/Demerzel#473**.

This file is the **navigable index over the work** — not a second backlog. Three sources feed it; this roadmap maps them into one hierarchy:

| Source | What it holds | This roadmap's job |
|---|---|---|
| [`BACKLOG.md`](../BACKLOG.md) | Ideas waiting to become features (Guitarist Problems, Prime Radiant, Pro-Guitarist Gaps, Chatbot Track P0–P2) | Group ideas under epics |
| [`docs/plans/`](plans/) | Active, in-flight plans | Link from the relevant story |
| GitHub Issues | Executable, grabbable work | Map each open issue to an epic/story |

So any contributor — human or AI worker — can navigate from *"where are we going"* (Epic) → *"what slice"* (Story) → *"what do I grab next"* (Issue).

## Hierarchy

```text
PI  (this file — the program increment)
  └─ Epic     major capability                 [Epic]
       └─ Story    user-facing slice           [Story]
            └─ Task     concrete implementation [Task]
                 └─ Subtask  file/test/checklist[Subtask]
```

**Triage labels** (see [`docs/agents/triage-labels.md`](agents/triage-labels.md)): `ready-for-agent` (fully specified, AFK-safe) · `ready-for-human` (needs a decision/design/verification first) · `needs-info` · `wontfix`.

**Verification reality (2026-06):** .NET build is policy-gated in some AI sandboxes, but the **frontend toolchain (`npm run build`/`lint`/`tsc`/`vitest`) runs locally** — so frontend-heavy stories (Epic 1–2) are the most AFK-friendly today; pure-C# stories generally need a build-capable worker.

---

## Epic 1 — Product architecture & app shell

**Goal:** a coherent React/Vite/Jotai shell with clean feature boundaries, a documented state model, and explicit integration seams to the backend services. Healthy foundation everything else mounts on.

| Story | Open issues / backlog | Status |
|---|---|---|
| App shell, routing & feature boundaries | `BACKLOG.md` → Prime Radiant infra | ready-for-human |
| Governance/observability panels | [#50](https://github.com/GuitarAlchemist/ga/issues/50) GovernanceMetricsDashboard · [#49](https://github.com/GuitarAlchemist/ga/issues/49) GovernanceCompliancePanel · [#56](https://github.com/GuitarAlchemist/ga/issues/56) AdminInbox→Demerzel | ready-for-human |
| Federation / data-backend panels | [#47](https://github.com/GuitarAlchemist/ga/issues/47) Prime Radiant ← ix `governance.graph` · [#52](https://github.com/GuitarAlchemist/ga/issues/52) AgentSpectralPanel · [#53](https://github.com/GuitarAlchemist/ga/issues/53) KnowledgeGraphPanel ← TARS · [#51](https://github.com/GuitarAlchemist/ga/issues/51) Discord→Prime Radiant WS | ready-for-human |
| App-level architecture doc + route smoke tests | _(task — unfiled)_ | ready-for-agent (frontend-verifiable) |

## Epic 2 — Guitar visualization & interaction

**Goal:** fast, correct 2D/3D rendering of fretboard, chords, scales, and hand pose — with clear ownership between the React/R3F path and the Godot path.

| Story | Open issues / backlog | Status |
|---|---|---|
| 3D scene performance (Prime Radiant / R3F) | [#42](https://github.com/GuitarAlchemist/ga/issues/42) InstancedMesh nodes (+6–12 FPS) · [#43](https://github.com/GuitarAlchemist/ga/issues/43) bake skybox to cubemap (+2–4 FPS) | **ready-for-agent** (frontend-verifiable locally) |
| Godot scene integration / inspection | [#54](https://github.com/GuitarAlchemist/ga/issues/54) GodotSceneInspectorPanel | ready-for-human |
| Live fretboard overlay | `BACKLOG.md` → `fretboard-overlay-live`, `chord-diagram-inline` | ready-for-human |
| Rendering ownership boundaries (2D vs 3D, WebGPU/Three/Pixi) | _(task — unfiled)_ | ready-for-human |

## Epic 3 — Audio & music-intelligence UX

**Goal:** make the chatbot/theory engine genuinely usable by a working guitarist — the largest and most differentiated surface. The detailed, prioritized work already lives in `BACKLOG.md`; this epic is its index.

| Story | Backlog track | Status |
|---|---|---|
| Chatbot trust (load-bearing) | Chatbot Track **P0**: `memory-session-scope`, `router-quality` | ready (tribunal-gated) |
| Capability completeness | Chatbot Track **P1**: `extended-chord-support`, `chord-identify`, `transpose-capo`, `voice-leading-pair`, `alternate-tuning-voicings`, `text-embedder-evaluation` | ready (tribunal-gated) |
| Advanced theory & arrangement | Chatbot Track **P2**: `modal-key-identify`, `modulation-detection`, `style-progression-gen`, `lead-sheet-arranger` | ready / blocked-by-deps |
| Pro-guitarist dealbreakers | `BACKLOG.md` → "Pro-Guitarist Usability Gaps" | ready |
| North-star problems | `BACKLOG.md` → "Guitarist Problems to Solve" | shaping |

> Epic 3 work that touches `GA.Business.ML/Agents/**`, MCP tooling, or the DSL parser is **tribunal-gated** (`Scripts/check-chatbot-tribunal-gate.ps1`) and routed via `/chatbot-iterate`, not picked ad hoc.

## Epic 4 — AIW integration for product work

**Goal:** consume Demerzel AIW-generated issues, keep product issues "Matt-ready" (scoped, testable, evidence-bearing), route implementation to the right worker (local/cloud/build-capable), and write product-specific evidence artifacts.

| Story | Open issues / artifact | Status |
|---|---|---|
| AIW intake + evidence/test contract | **new:** [`docs/workflows/ga-aiw.md`](workflows/ga-aiw.md) | ✅ shipped with this PR |
| Skills-driven issue quality | [#481](https://github.com/GuitarAlchemist/ga/issues/481) use Matt Pocock skills for scoped slices | ready-for-human |
| Autonomous-loop evidence | [#428](https://github.com/GuitarAlchemist/ga/issues/428) first real `/auto-optimize` iteration + ix maintain-gate | ready-for-human (needs live oracle) |
| CI signal honesty | [#328](https://github.com/GuitarAlchemist/ga/issues/328) chatbot-QA snapshot degradation | ready-for-agent (workflow override required) |

---

## How to pick work

1. Find the Epic that matches the capability you're advancing.
2. Within it, prefer a **`ready-for-agent`** issue if you're an AI worker (or running AFK); `ready-for-human` items need a decision/design/verification from a person first.
3. For Epic 3 (chatbot/theory): don't hand-pick — go through `/chatbot-iterate`, which reads the `BACKLOG.md` Chatbot Track and enforces the tribunal gate.
4. New idea, not yet a slice? Add it to `BACKLOG.md` and run `/feature <idea>` (brainstorm → plan → PR).

## Completion criteria (#482)

- [x] `ga` has a navigable roadmap (this file) that indexes BACKLOG + plans + issues under PI→Epic→Story.
- [x] Product tasks can link to Demerzel AIW issues when implemented by AI workers (Epic 4 + `docs/workflows/ga-aiw.md`).
- [x] Product issues have clear scope, demo/evidence, and test expectations (codified in `docs/workflows/ga-aiw.md`).

> **Living document.** When an epic's stories shift, update the tables here in the same PR. The roadmap is only useful while it tracks reality.
