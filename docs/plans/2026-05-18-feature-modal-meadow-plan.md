---
title: Modal Meadow — interactive mode-region 3D demo
created: 2026-05-18
owner: spareilleux
status: v0 in-flight (this PR delivers v0)
revisit_trigger: 30 days of `/test/fluffy-grass` engagement signal OR direct user feedback that the modal-region metaphor doesn't read
reversibility: easy — fully isolated under `/test/modal-meadow`; deletion is a route + folder removal; no shared schema, no cross-repo dependency, no public API change
---

# Modal Meadow — interactive mode-region 3D demo

## Problem framing (Karpathy R5 — who is in pain, what changes)

GA's music-theory engine is, today, mostly **text and tables**. The chord/scale/mode systems live in:

- the chat panel (`GAChatPanel`)
- the diatonic chord table (`DiatonicChordTable`)
- the BSP / hierarchy demos under `/test/bsp` and `/test/music-hierarchy`
- the AG-UI streaming responses from the backend

For a beginner who doesn't yet know what *Phrygian* sounds like or what it *feels* like, none of these surfaces is visceral. They explain mode *theory* but not mode *colour*.

**Who is in pain:** a learner who can read "Phrygian is dark and modal, evokes Spanish and Middle-Eastern idioms" but has no way to feel what that means without already having internalised the sound.

**What changes:** Modal Meadow turns each mode into a *region of a grass field*. Walking from one to the next:

- swaps the ambient chord progression (sonic colour)
- repaints the grass (visual colour)
- shifts wind behaviour (kinetic colour)

The learner doesn't read about Phrygian; they walk into Phrygian and the world becomes Phrygian. The metaphor is the lesson.

This is a **GA-shaped problem**: we already render grass (`/test/fluffy-grass`), we already have mode primitives in the BSP `musicalTree.ts`, and we already have a first-person walker pattern (`/test/immersive-musical-world`). Modal Meadow is the recombination, not a new capability.

## Prior art in this codebase

| What | Where | What we take |
|---|---|---|
| Cinematic instanced grass + bezier blades + wind shader | `src/components/FluffyGrass/FluffyGrass.tsx` | Shader structure (NOISE_GLSL, gust band, bezier bend) — we re-implement at larger scale rather than parameterising the existing demo (hard constraint: don't touch `/test/fluffy-grass`) |
| First-person WASD + pointer-lock | `src/components/BSP/ImmersiveMusicalWorldDemo.tsx` | Movement pattern + ESC/click semantics |
| Mode names + diatonic-step arrays (`Ionian`, `Dorian`, `Aeolian`, `Mixolydian`) | `src/components/BSP/v2/musicalTree.ts` | Step intervals for chord-building. Phrygian is not in that file's enum — we extend locally rather than mutate the BSP code. |
| Demo-page boilerplate (route, `TestIndex` card, ErrorBoundary) | `src/main.tsx`, `src/pages/TestIndex.tsx` | Pattern only |

## v0 scope — THIS PR

Hard cap: ship a credible, interactive 2-region experience in one PR. Everything else is followups.

- **Two mode-regions only**: Ionian (left half, west) and Phrygian (right half, east), separated by a soft transition band centred at x=0 with ~10m feathered width.
- **First-person walker**: pointer-lock + WASD, fixed eye-height at y=1.7m, walk speed ~5 m/s. No jumping, no gravity, no physics.
- **Field scale**: ≥ 200m × 200m worldspace, ≥ 4× original blade count. LOD allowed: far blades sparser.
- **Visual mode-cueing**:
  - Ionian: warm yellow-green grass (`#a8d670`-ish blade tip, `#3a6b2a`-ish base), gentle wind (windSpeed 0.6, windStrength 0.25), all blades upright, sky soft golden.
  - Phrygian: dusky green-with-violet-hints (`#5a6b5a`-ish blade tip, `#1d2a1a`-ish base, with violet shimmer), aggressive wind (windSpeed 1.6, windStrength 0.55), some blades drooping (extra base bend), sky cooler.
  - Smooth lerp across transition band — shader uniform `uModeMix` in [0,1].
- **Audio**: Web Audio API (no library — none is in `package.json` and adding one is a separate decision).
  - Ionian: I–IV–V–I in C major (C–F–G–C) at ~4s per chord, soft sine+triangle pad voices.
  - Phrygian: i–bII–bvii–i in E Phrygian (Em–F–Dm–Em) at the same tempo, slightly darker timbre.
  - Crossfade by region weight derived from camera x. Ambient level ≈ –20 dB.
  - Audio resumes only after first click (browser autoplay policy).
- **HUD**: bottom-left panel shows current mode name + one-line "feel" descriptor; centre-top hint "Click to enter / WASD to walk / ESC to release" when not pointer-locked.
- **No regression on `/test/fluffy-grass`**: it remains literally unchanged.

### v0 success criteria

1. `/test/modal-meadow` route renders without console errors in Chrome.
2. First-person camera walks via WASD + mouse-look.
3. Two visually distinct grass regions ≥ 50m apart along x.
4. Crossing region boundary smoothly fades ambient audio Ionian↔Phrygian.
5. `npm run build` succeeds.
6. `npm run lint` passes.
7. `/test/fluffy-grass` continues to render with no visible change.

## v1 scope — followup PR

- All **7 diatonic modes** as regions of a 7-sector wheel (Ionian, Dorian, Phrygian, Lydian, Mixolydian, Aeolian, Locrian) centred on the player's spawn point.
- **Per-mode visual choreography** (the big design question — see below):
  - Lydian: bright, slightly other-worldly (extra blue in tip colour), tall blades, fewer flowers, occasional vertical shimmer (#4 mode tension visualised).
  - Mixolydian: amber-tinted, slightly more wind, denser flowers.
  - Aeolian: cool blue-green, slower wind, more shadow.
  - Dorian: ambiguous — same hue family as Aeolian but more flowers (modal lift).
  - Locrian: visibly unstable — sparse, faint chromatic shimmer, low blade density, sky overcast.
- **Discoverable mode-info card** when entering a region: name, parent scale, character notes, diatonic triads, common progressions — pulled from `musicalTree.ts` rather than re-typed.
- **Audio**: one progression per mode; transition handled by 7-way audio bus with weight = inverse-distance to each region centre.
- **Mini-map** showing the wheel and the player position.

## v2 scope — further followup

- **MIDI input** → the user plays chords on a connected controller; grass responds (a Cmaj chord makes Ionian region's blades pulse; an Em with b9 visibly bends the field).
- **Mood quest gameplay loop**: a goal text appears ("Find the mode that feels like longing"), the player walks, the audio shifts, the goal resolves when they stop in the right region long enough.
- **Hidden collectibles** — secret chords scattered, picking one unlocks the corresponding mode's wallpaper / colour theme for the rest of the demos site.
- **Multiplayer ghost overlay** — see other learners' paths through the modes (rendered as faded silhouettes).

## Architecture sketch

```
ReactComponents/ga-react-components/src/
├── pages/
│   ├── ModalMeadowTest.tsx              [NEW, page wrapper with ErrorBoundary]
│   └── TestIndex.tsx                    [MODIFIED, add card]
├── components/
│   └── ModalMeadow/
│       ├── ModalMeadow.tsx              [NEW, main R3F-free Three.js scene]
│       ├── modes.ts                     [NEW, per-mode config — colours, wind, progression]
│       ├── audio.ts                     [NEW, Web Audio polyphonic pad + two-bus crossfade]
│       └── index.ts                     [NEW, barrel]
└── main.tsx                             [MODIFIED, register /test/modal-meadow route]
```

Three.js is used directly (not R3F) to mirror `FluffyGrass.tsx` — that file is the canonical pattern in this codebase for procedural-shader demos. R3F sits in `@react-three/drei` and `@react-three/fiber` deps but the demos consistently bypass it for raw shader work.

**Audio engine**: Web Audio API only. Two `GainNode`s feed a master gain; each is fed by a small polyphonic pad built from `OscillatorNode` (sine + detuned triangle, low-pass). Chord progressions are looped in JS with `setTimeout`. Volume crossfade is a single read of camera.position.x → mix weights.

**Single canvas, single scene, single renderer.** No SSR, no R3F, no postprocessing in v0 (skip bloom — the demo emphasises mode comparison, not cinematic).

## Cost breakdown

| Phase | Effort | Notes |
|---|---|---|
| v0 (this PR) | ~1 day | Plan + 2-region demo + audio crossfade |
| v1 | ~2–3 days | 5 more regions + mode-info card + per-mode choreography + 7-bus audio |
| v2 | ~3–5 days | MIDI input, gameplay loop, collectibles, ghost overlay |
| **Total fully built** | **~6–9 days** | |

## Reversibility + revisit trigger (Karpathy R6)

- **Reversibility**: easy. Modal Meadow lives entirely in `src/components/ModalMeadow/` and a single route. Deletion is a 4-file revert + one-line route removal. No DB schema, no cross-repo contract, no public API.
- **Revisit trigger**:
  - 30 days after merge, check `/test/fluffy-grass` and `/test/modal-meadow` engagement (if any analytics surface exists — TBD).
  - **Or** direct user feedback during v0 review that the metaphor doesn't land. If the user says "the mode shift isn't obvious enough" or "the regions feel arbitrary", we revisit before v1 with a stronger differentiator (maybe audio-only first, visuals reactive to audio FFT).
- **Not a one-way door**: dimension constants, shared schemas, public APIs are untouched. The visual choreography in v1 *will* require some tuning that's hard to undo cleanly (e.g., committing to "Locrian = sparse + overcast" is a creative-direction decision), so the v1 PR should be reviewed as a creative artefact, not just code.

## What we deliberately don't do in v0

- **No new dependency in `package.json`**. Tone.js would simplify audio code but the user explicitly said "DO NOT add a new audio library dependency in this PR".
- **No MIDI input**. v2.
- **No mode-info modal card**. Just a HUD line. v1.
- **No fluffy-grass parameterisation**. The existing demo is touched zero. Modal Meadow re-implements the grass it needs at the scale it needs.
- **No physics, no jumping, no collision**. Walk speed × dt, fixed eye height. Done.

## Open creative questions for v1 (recorded so we don't forget)

1. Should mode boundaries be **discrete** (you cross a line, the audio swaps) or **continuous** (you're 60% Phrygian, 40% Aeolian)? v0 is continuous because that fits the metaphor (mode colours blend at borders, like ecotones).
2. How do we visualise modes that **share a parent scale** (Dorian / Phrygian / Aeolian are all from the C-major collection)? Adjacent regions? Different "depths"? An overlapping shimmer?
3. Does Locrian deserve its own region or is it *always* the "unstable border zone"? Music-theoretically Locrian's tritone-root issue makes it a transition region as much as a destination.

These are the questions we'll bump into the second we go past 2 modes — calling them out now so they don't ambush v1.
