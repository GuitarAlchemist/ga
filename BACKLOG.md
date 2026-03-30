# Guitar Alchemist Backlog

Ideas waiting to become features. One bullet per idea. When ready to build, run `/feature <idea>` — this launches brainstorm → plan → PR.

See `docs/plans/` for active plans.

---

## Guitarist Problems to Solve

These are real problems guitarists hit. They're the North Star for every feature.

### Ear Training & Recognition
- **"What key am I in?"** — a guitarist plays a progression by ear and wants GA to identify the key, suggest the scale, and show the diatonic chord set
- **"Why does this sound outside?"** — given a chord or note over a backing, explain which scale degrees are "outside" and why (tension vs. resolution)
- **"Is this a common substitution?"** — given two chords, tell the guitarist if there's a known substitution relationship (tritone sub, backdoor dominant, parallel minor, etc.)

### Chord & Voicing Discovery
- **"My hand hurts playing barre chords"** — suggest open-position or partial-barre voicings for any chord, ranked by fret-hand stretch
- **"I play in DADGAD / open D / open G"** — generate correct chord shapes for any alternate tuning, with voicing diagrams

### Improvisation & Scale Choice
- **"Which arpeggio fits this chord progression?"** — given Am F C G, suggest the arpeggios and scales that work over each chord with mode names
- **"How do I solo over a ii-V-I?"** — step-by-step: target notes, guide tones, chromatic approaches, bebop scale options
- **"Show me a lick in the style of [blues / jazz / country]"** — generate a 2-bar lick in ASCII tab + VexTab matching the stylistic vocabulary

### Practice & Learning
- **"I want to practice this scale in all positions"** — generate all 5 CAGED positions for any scale, with tab + fretboard diagram
- **"I keep forgetting chord tones"** — a drill: show a chord symbol → user names the intervals → GA verifies with GaChordIntervals
- **"How long until I can play this song?"** — technique gap analysis: compare required techniques to user's known skills, suggest a practice sequence

### Songwriting & Composition
- **"Help me finish this progression"** — given 2-3 chords, suggest 2-3 natural completions that cadence correctly, in the same key
- **"Make this progression more interesting"** — apply passing chords, secondary dominants, or borrowed chords to a plain I-IV-V
- **"What would this sound like in a minor key?"** — parallel minor / relative minor translation of an existing progression

### Technical / Gear
- **"How do I tune to drop-C?"** — fret-by-fret retuning guide with string tensions and chord shape adjustments
- **"Why does my tab look wrong?"** — parse ASCII tab and flag common notation errors (string order, timing symbols, missing barlines)

---

## Prime Radiant / Living Cosmos Ideas

### Shipped (2026-03-28 mega session)
- ~~IXQL data pipeline (DataFetcher + DynamicPanel)~~ → Phase 2
- ~~Declarative health bindings (HealthBindingEngine)~~ → Phase 3
- ~~Reactive triggers (ON...THEN + ReactiveEngine)~~ → Phase 5 MVP
- ~~Demo tour button (yellow lightning, 6-step walkthrough)~~
- ~~Edge highlighting (SELECT edges SET color/width)~~
- ~~Triage Drop Zone (drag/paste for AI classification + dispatch)~~
- ~~Algedonic → triage wiring (📥 on recommended actions)~~
- ~~JPP comics inline PDF reader (16 comics, Public Domain)~~
- ~~Seldon beliefs + Markov predictions populated~~
- ~~/devfix skill + session-start health check~~
- ~~[SHIPPED] Admin-only access (#30)~~
- ~~[SHIPPED] Rich hover popovers (#32)~~
- ~~[SHIPPED] LLM Status panel real checks (#33)~~
- ~~[SHIPPED] Backlog beliefs with AI assessment (#34)~~
- ~~[SHIPPED] Brainstorm button / What's Next (#36)~~
- ~~[SHIPPED] Godot integration plan (#37)~~
- ~~[SHIPPED] JPP Library panel (#38)~~
- ~~[SHIPPED] Health→graph visualization (#39)~~
- ~~[SHIPPED] Epistemic Constitution + CognitionModel~~
- ~~[SHIPPED] IXQL epistemic commands~~
- ~~[SHIPPED] Meshy AI MCP server~~
- ~~[SHIPPED] Blue-green build MSBuild hook~~
- ~~[SHIPPED] Mobile phone layout fixes~~
- ~~[SHIPPED] GIS preset fixes~~

### Shipped (2026-03-29 multi-model orchestration session)
- ~~Godot Bridge Phase 1 (bridge protocol, useGodotBridge, GodotScene, A2A agent presence)~~
- ~~Grouped IconRail with LED status indicators (5 groups, 22 panels)~~
- ~~LLM Provider categories with LEDs (8 providers: Cloud AI / Local / Tools)~~
- ~~Mistral guitar-alchemist agent (cross-model theory validator)~~
- ~~Multi-Model Fan-Out (parallel provider query service)~~
- ~~Theory Tribunal (multi-model music theory consensus panel)~~
- ~~Demerzel Voice (VoxtralTTS pipeline with Godot lip sync)~~
- ~~Seldon Faculty (LLM providers as university department heads)~~
- ~~Code Lab (multi-model code generation with diff view)~~
- ~~Signal Creation Form (create algedonic signals from UI)~~
- ~~Mobile Overflow Menu (bottom sheet drawer for 22 panels)~~
- ~~Admin Inbox (triaged items with approve/reject/defer)~~
- ~~Weak Signal Interaction Graph (canvas force graph)~~
- ~~Screenshot Capture (camera button + preview overlay)~~
- ~~Active Teams accordion (Claude Code agent teams in ActivityPanel)~~
- ~~FE screenshot capture via Demerzel~~
- ~~Claude teams accordion~~
- ~~Voxtral/Codestral/ACP Vite proxy with auth headers~~

### Active Ideas
- **3D animated IX pipeline flow** — click IXql node → expands into animated pipeline with particles flowing through stages. Uses IxqlParser.
- **Commit detail hover** — hover a commit in ActivityPanel shows diff stats, files changed, author
- **GitHub token for API rate limits** — authenticated requests (5,000/hr vs 60/hr) for the multi-repo ActivityPanel
- **Progressive pipeline zoom** — far = IXql node, medium = orbiting sub-nodes, close = full animated flow diagram
- **Algedonic channels** — Demerzel builds real-time pleasure/pain signal channels from governance health, updates belief states live via SignalR
- **Octopus PR review queue** — industrial-scale PR review using /octo:security + /octo:debate across multiple LLMs, results surfaced in ActivityPanel
- **Auto-spawn Claude Code teams** — Demerzel detects workload (open issues, failing tests, stale nodes) and auto-spawns Claude Code agent teams to address them
- **Activities from real sources** — wire Activities accordion to actual task tracking (GitHub Projects, Linear, or internal PDCA cycles from governance state)
- **Terminal node filaments** — leaf/terminal nodes emit thin glowing filaments with lit dots at the end

### New Ideas (from 2026-03-28 session)
- **IXQL Phase 6: FALLBACK + BACKOFF** — chaos resilience: `CREATE PANEL ... FALLBACK source2 BACKOFF exponential 5s..300s TOLERANCE 3`
- **IXQL meta-combination** — `panel://` cross-references so panels can read from other panels' data
- **Grammar telemetry dashboard** — visualize IXQL variant adoption rates from localStorage telemetry
- **Triage → IXQL auto-generation** — when an item is dispatched, generate the IXQL command to create it (e.g. task → CREATE PANEL with the task data)
- **Godot MCP bridge** — DataFetcher already has `godot://` protocol, wire it to 3D scene management
- **Octopus plugin reinstall** — `/octo:*` skills not loading, needs `claude plugin install octo@nyldn-plugins`

## Infrastructure Ideas

- **Live fretboard overlay** — show scale degrees on the React 3D fretboard in real time as the chatbot explains a concept
- **Chatbot chord diagram rendering** — when TheoryAgent mentions a chord, auto-generate a VexTab diagram inline in the chat response
- **BSP room chord assignment** — assign a diatonic chord function (I, ii, V…) to each BSP room and visualise harmonic flow through rooms
- **OPTIC-K similarity search UI** — let guitarists search by playing a rhythm pattern, find songs with similar harmonic structure

---

## How to Start a Feature

```bash
/feature <idea from above>
```

The `/feature` skill will:
1. Brainstorm with GA MCP tools to verify the music theory
2. Produce a plan in `docs/plans/`
3. Guide implementation grounded in the GA domain model
