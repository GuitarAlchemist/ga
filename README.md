# Guitar Alchemist

AI-augmented music theory and guitar learning platform — **.NET 10 / C# 14 backend**, **F# DSL** for music theory primitives, **React 18 + Vite frontend**, Aspire-orchestrated. Part of a four-repo ecosystem (`ga` + [`ix`](https://github.com/GuitarAlchemist/ix) Rust ML + [`Demerzel`](https://github.com/GuitarAlchemist/Demerzel) governance + [`tars`](https://github.com/GuitarAlchemist/tars) F# theory validator).

> **Agent-facing canonical docs:** [`CLAUDE.md`](./CLAUDE.md) (~70 lines, breadcrumb-style) and [`AGENTS.md`](./AGENTS.md) (full development guidelines).

## Live demos

- [**Chatbot**](https://demos.guitaralchemist.com/chatbot/) — music theory Q&A with skill-routed responses (chord voicings, modes, scales, voice-leading, circle of fifths, improvisation choices, …) and a live agentic trace.
- [**Inverse Kinematics**](https://demos.guitaralchemist.com/test/inverse-kinematics) — 3D anatomically-modeled left hand on a fretboard, MCP-controllable via `window.__gaIK`.
- [**Prime Radiant**](https://demos.guitaralchemist.com/test/prime-radiant) — 3D governance + solar system visualization with Demerzel AI overlay.
- [**Component test pages**](https://demos.guitaralchemist.com/test) — fretboard, OPTIC-K embedding browser, nature simulations, more.

## Architecture (breadcrumb)

Strict bottom-up five-layer model:

```
1. Core         — GA.Core, GA.Domain.Core (Note, Interval, Fretboard primitives)
2. Domain       — GA.Business.Core, GA.Business.Config, GA.BSP.Core (logic + YAML)
3. Analysis     — GA.Business.Core.Harmony, GA.Business.Core.Fretboard (voice leading, geometry, spectral)
4. AI / ML      — GA.Business.ML (embeddings, OPTIC-K, RAG, chatbot skills)
5. Orchestration — GA.Business.Core.Orchestration, GA.Business.Assets, GA.Business.Intelligence
```

**Rule: AI code lives in layer 4. Orchestration in layer 5. Never in lower layers.** Full layer map: [`docs/architecture/layers.md`](./docs/architecture/layers.md).

### Key concepts

- **OPTIC-K v1.8** — 240-dim musical embedding (124-dim compact form on disk). Schema constants in `Common/GA.Business.ML/Embeddings/EmbeddingSchema.cs` — read `TotalDimension` / `Version`, never hardcode. Changing dimension is a one-way door.
- **Voicing-search RAG** — chord-voicing lookup over OPTIC-K geometry, powering the chatbot's `ChordVoicingsSkill` and the typed `MusicalQueryEncoder` pipeline.
- **Grothendieck δ** — harmonic-distance metric between voicings, used by `GrothendieckDeltaSkill` and the fretboard shortest-path solver.

## Build, test, verify

```powershell
dotnet build AllProjects.slnx -c Debug                       # full build
dotnet test  AllProjects.slnx                                # full test suite
pwsh Scripts/start-all.ps1 -Dashboard                        # start everything via Aspire
pwsh Scripts/run-all-tests.ps1 -BackendOnly -SkipBuild       # faster backend-only loop
pwsh Scripts/start-dev.ps1                                   # GaApi on 5232 + Vite on 5176 with auto-restart
```

For frontend work: `npm run build && npm run lint` in `ReactComponents/ga-react-components`.

## Local dev stack (default ports)

| Service | Port | Purpose |
|---|---|---|
| GaApi (.NET) | 5232 | REST + GraphQL, mounts the chatbot at `/chatbot/*` via PathBase reverse-proxy |
| GaChatbot.Api (.NET) | 5252 | Skill-routed chatbot backend — runs side-by-side with GaApi |
| Vite (React) | 5176 | Frontend dev server with HMR |
| Aspire Dashboard | 15001 | Service health + traces |

See [`reference_dev_stack_three_services`](./docs/runbooks/dev-stack-three-services.md) for the three-service split and the cloudflared route configuration for `demos.guitaralchemist.com`.

## AI discipline (Karpathy + Cherny)

Every code-touching turn applies six rules: **think before coding · simplicity first · surgical changes · verifiable success criteria · frame problem before solution · instrument one-way doors.** Session continuity uses the Cherny pattern: `/digest` writes `state/digests/latest.md` at breakpoints; `/learnings` captures surprises to `docs/solutions/`; `/auto-optimize` drives Cherny-style improvement loops per domain (chatbot-qa, embeddings, voicing-analysis).

CI enforces this in [`.github/workflows/karpathy-cherny-discipline.yml`](./.github/workflows/karpathy-cherny-discipline.yml).

## Multi-LLM review (load-bearing)

For music-theory / DI / parser / MCP changes, code review fans out to multiple LLMs in parallel: `octo:droids:octo-code-reviewer` + `octo:droids:octo-security-auditor` + (when available) `octo:droids:octo-performance-engineer`. This has caught 9+ real bugs in past chatbot-migration PRs that local tests missed. See `docs/methodology/multi-llm-review.md`.

## Quality cadence

Daily CI workflows snapshot quality signals to `state/quality/<domain>/YYYY-MM-DD.json`:

- [`chatbot-qa-snapshot.yml`](./.github/workflows/chatbot-qa-snapshot.yml) — corpus pass-pct, by category, latency.
- [`embeddings-snapshot.yml`](./.github/workflows/embeddings-snapshot.yml) — OPTIC-K leak-detection, retrieval consistency, clustering, topology (via `ix-embedding-diagnostics` in the ix sibling).

Trend dashboard at `ix-quality-trend` aggregates these snapshots cross-domain.

## Key features

- **Music theory engine** — chord, scale, key, mode, interval, voicing, set-class, Grothendieck δ, voice-leading.
- **Skill-routed chatbot** — 25+ deterministic skills (chord voicings, modes, scales, capo, alternate tunings, ICV neighbors, voice leading, circle of fifths, beginner chords, improvisation choices, …) with a semantic-router fallback.
- **OPTIC-K embeddings** — partition-weighted 240-dim geometry over voicings; RAG, leak diagnostics, daily quality cadence.
- **F# DSL for music theory** — `GA.Business.DSL` exposes the primitives in a type-safe scriptable form.
- **3D demos** — inverse kinematics (anatomical left hand), fretboard, Prime Radiant governance visualization.
- **Cross-repo orchestration** — MCP federation with `ix` (Rust ML), `Demerzel` (governance + ACP), `tars` (F# theory validator).

## AI surfaces

Three coexisting agent surfaces in this workspace: **Claude Code** (primary), **Antigravity native**, **Augment**. Hand-off via `Scripts/antigravity-bridge.ps1`. Split documented in [`docs/methodology/ai-surfaces.md`](./docs/methodology/ai-surfaces.md).

## ix ML federation

GA exposes [ix's Rust ML toolkit](https://github.com/GuitarAlchemist/ix) via MCP federation. Useful entry points:

- `ix_ml_pipeline` — one-call ML pipeline: classify progressions, cluster voicings, analyze harmonic complexity.
- `ix-embedding-diagnostics` (binary: `baseline-diagnostics`) — partition-by-partition leak detection on the OPTIC-K corpus.
- Skills: `/ix-ml-builder`, `/federation-music`, `/federation-discover`.

All operations governed by the [Demerzel](https://github.com/GuitarAlchemist/Demerzel) epistemic constitution.

## Project layout

```
ga/
├── Apps/
│   ├── GaApi/                       # Main API (REST + GraphQL, port 5232)
│   └── GaChatbot.Api/               # Skill-routed chatbot (port 5252)
├── Common/
│   ├── GA.Business.Core/            # Layer 1 — pure theory primitives
│   ├── GA.Domain/                   # Layer 2 — domain types
│   ├── GA.Business.Core.Analysis/   # Layer 3 — geometry + analysis
│   ├── GA.Business.ML/              # Layer 4 — AI / OPTIC-K / skills
│   ├── GA.Business.DSL/             # F# music-theory DSL
│   └── GA.Business.Core.Orchestration/ # Layer 5 — DI + plugins
├── ReactComponents/ga-react-components/ # Vite frontend (port 5176)
├── Tests/                            # NUnit / xUnit / Playwright
├── docs/
│   ├── architecture/                # Layer map + design notes
│   ├── methodology/                 # AI surfaces, multi-LLM review
│   ├── plans/                       # Active feature plans
│   ├── solutions/                   # Cherny-style past learnings
│   ├── contracts/                   # Cross-repo schemas
│   └── runbooks/                    # Operational procedures
├── state/
│   ├── digests/                     # Session continuity (Cherny)
│   ├── quality/                     # Daily quality snapshots
│   └── voicings/optick.index        # OPTIC-K corpus (~175 MB)
├── governance/demerzel/             # Submodule — Demerzel constitution
└── Scripts/                         # PowerShell tooling
```

## Acknowledgements

Voice-leading geometry features are inspired by:

- Dmitri Tymoczko. 2011. *A Geometry of Music: Harmony and Counterpoint in the Extended Common Practice.* Oxford University Press.
- Clifton Callender, Ian Quinn, Dmitri Tymoczko. 2008. "Generalized Voice-Leading Spaces." *Music Theory Online* 14(3).
- Alexander Grothendieck — categorical foundations behind the harmonic-distance metric used in `GrothendieckDeltaSkill` and the fretboard shortest-path solver.

See [`REFERENCES.md`](./REFERENCES.md) for full citations and implementation notes on OPTIC (Octave, Permutation, Transposition, Inversion, Cardinality) quotienting.

## License

MIT — see [`LICENSE`](./LICENSE).

## Links

- [Aspire dashboard](https://localhost:15001) — service monitoring (when running locally)
- [Live demos](https://demos.guitaralchemist.com/) — public preview
- Issues, PRs, discussions: [GitHub](https://github.com/GuitarAlchemist/ga)
