# OPTIC-K Corpus Visualization — "Harmonic Nebula"

**Status:** Proposed — awaiting sign-off before scaffolding
**Date:** 2026-04-20
**Owner:** spareilleux
**Reversibility:** Two-way door for the demo code itself; **one-way door** for the cluster-centroid layout algorithm and the pitch-class merge semantics (once a user mental model is established, those become public contract).
**Revisit trigger:** If user study shows < 60% task completion on "where does Cmaj7 live?" within 10 seconds of first view, restart from metaphor.

## Problem

The GA ecosystem has an OPTIC-K v1.8 corpus of 313,047 voicings (297,910 guitar / 7,795 bass / 7,342 ukulele) with 124-dim embeddings, k=50 clusters, and pairwise transition costs. There is no way for a user to see, navigate, or query this corpus visually. Prime Radiant is a governance-graph viz, not a corpus viz.

The target user is a **musician**, not an ML researcher. The viz must answer three questions in under 10 seconds:

1. *Where does Cmaj7 live?*
2. *Which voicings are similar to this one?*
3. *What's rare vs common?*

With zero ML glossary. The words "embedding", "UMAP", "silhouette", "betti" never appear in the UI.

## Decision

Build **Harmonic Nebula** — a hybrid two-level visualization:

- **Macro**: 50 cluster centroids rendered as dense nebular clouds, positioned via UMAP over the **transition-cost graph** (not the embedding). Clouds that are close mean "voicings here transition smoothly to voicings there" — musically meaningful proximity. Filaments of faint dust bridge nearby clouds.
- **Micro**: inside each cloud, individual voicings appear as instanced stars positioned by local UMAP on the 124-dim embedding, normalized per cluster.
- **Motion**: idle shimmer only (no chaotic drift). Selection triggers a transient "harmonic wind" — particle streamlets along transition-cost paths to the top-5 nearest voicings.

**This is option (e) from the design brainstorm, refined by adversarial synthesis across Codex, Gemini, and Claude.** See the Multi-AI Input section for which specific ideas came from where.

## Data mapping

| Visual channel | Encodes | Source |
|---|---|---|
| Cloud position | Transition-cost proximity between clusters | UMAP/spectral layout over `ga/state/voicings/*-transitions.json` |
| Star position (inside cloud) | Embedding similarity within cluster | Local UMAP on `optk-v4-pp-r` embedding, normalized per cluster |
| Hue | Chord quality family (major / minor / dom / dim / sus / altered) | `ChordIdentification.CanonicalName` parse |
| Saturation | Instrument (guitar strong, bass muted, ukulele bright) | `instrument` field |
| Lightness | Rarity (common = dim, rare = glowing) | Cluster density rank |
| Size | Selection/search emphasis only — NOT frequency (avoid visual hierarchy confusion) | Runtime |
| Motion | Idle shimmer; selection-triggered transition arcs | `*-transitions.json` top-K for selected voicing |

**Pitch-class set** is surfaced only in the detail panel on click, never in spatial position.

## Interaction model

- **Entry view**: nebula at cosmic scale, 50 clouds labeled with dominant chord quality ("maj7 cloud", "altered-dominant cloud").
- **Search**: typing `Cmaj7` dims non-matching voicings, highlights matching, camera flies to the densest matching cloud.
- **Click voicing**: detail card with chord name, instrument, fingering (if present), pitch classes, rarity percentile, and "nearby sounds" (top-5 transition neighbors). UI copy: **"Similar grips / sounds"**, never "nearest neighbors".
- **Filters**: instrument toggles, chord-family chips, rarity slider, "easy moves" mode (highlights low-transition-cost filaments).
- **Zoom behavior**: far = clouds only; mid = density shells + chord families; near = individual stars selectable.
- **Pitch-class merge toggle** *(critical, see Minority Opinion below)*: collapses all fingerings of the same chord into one superposed glyph with a count badge. Flips between "same sound" and "same grip" views. Defangs the embedding's fingering-sensitivity.

## Architecture

Four ThreeJS/TSL components:

1. **`VoicingInstanceCloud`** — single `InstancedMesh` for all 313k voicings. Per-instance attributes: position, color, size, cluster id, instrument id, rarity, chord family id, selection mask. TSL shader handles filter fade, glow, hover emphasis. Complexity: O(N) upload, one draw call.
2. **`NebularCloudLayer`** — 50 lightweight volumetric density fields / impostor blobs around clusters, built from precomputed density summaries (not marching cubes). Labels via screen-space HTML or SDF text.
3. **`TransitionFilamentLayer`** — GPU curve renderer for selected subset only. Centroid-to-centroid filaments globally; voicing-to-voicing arcs on selection. Animated particle streamlets along transition-cost paths. Never renders all pairwise transitions.
4. **`SpatialIndex + QueryStore`** — CPU-side typed arrays + cluster buckets. `CanonicalName → instance ids` lookup. Similarity via precomputed neighbor lists. Picking via color/id buffer or cluster-bucket BVH.

Reuses Prime Radiant's WebGPU/3d-force-graph plumbing where it fits; clean scene where it doesn't.

## Multi-AI input (provenance)

- **Codex (GPT-5)**: architectural skeleton, including the critical insight that cluster centroids should be laid out on the **transition-cost graph**, not the embedding. Musician-facing copy ("Similar grips/sounds"). Scoped component boundaries.
- **Gemini**: nebula metaphor (canonical naming), particle streamlets along transition paths on selection (adopted as transient effect on `TransitionFilamentLayer`). Also flagged the fingering-sensitivity tradeoff that motivated the pitch-class merge toggle.
- **Claude**: volumetric density shells, betti-visualization as optional overlay (banked for later), pitch-class merge toggle as synthesis of Gemini's tradeoff warning.

Synthesis logs at `ga/state/knowledge/2026-04-20-optick-viz-multi-ai-synthesis.md` *(to be written separately if requested)*.

## Phased implementation

### Phase 1 — offline precompute (1-2 days)

- Rust tool (or extend existing): read corpus + transitions + clusters, emit:
  - `state/viz/cluster-centroids.json` — 50 centroids with transition-cost-graph UMAP positions
  - `state/viz/voicing-offsets.bin` — 313k × (3 floats) of per-voicing local position
  - `state/viz/voicing-attrs.bin` — per-voicing packed attrs (cluster id, instrument, chord-family id, rarity rank)
  - `state/viz/neighbors.bin` — top-K transition neighbors per voicing (K=5 or 10)
- Commit deterministic output so the viz doesn't depend on runtime UMAP computation.

### Phase 2 — scene skeleton (3-5 days)

- New route `/demos/harmonic-nebula` in `Apps/ga-client`.
- Lazy-loaded `HarmonicNebulaDemo` component with `InstancedMesh` + `NebularCloudLayer`.
- TSL material with filter-fade + hover emphasis. No interactions yet — just render.
- Hit target: 60fps on M1 MacBook Air with all 313k instances visible.

### Phase 3 — interactions (3-5 days)

- Click picking, detail card, search by `CanonicalName`, instrument toggle.
- `TransitionFilamentLayer` rendering static centroid filaments.
- Camera zoom LOD.

### Phase 4 — selection effects + pitch-class merge (2-3 days)

- Animated particle streamlets on selection.
- PC-set merge toggle.
- Rarity slider.
- "Easy moves" mode.

Rough total: **2-3 weeks** with existing Prime Radiant plumbing reused; 4 weeks clean.

## Non-goals

- Not a replacement for Prime Radiant — that stays governance-focused.
- Not a real-time playback tool. No audio, no strumming. This is a **map**, not an instrument.
- Not a training tool. No exercises, no gamification.
- Not a betti-visualization (banked as optional overlay).
- No UMAP computation in the browser — always offline, always deterministic.

## Open questions (pre-sign-off)

1. Is `/demos/harmonic-nebula` the right route, or fold it into the existing Prime Radiant shell as a mode?
2. Should instrument filter be toggle (one at a time) or multi-select?
3. How do we handle voicings where `CanonicalName` is a Forte class (e.g., "Forte 5-32")? Hide from default view? Surface under "Uncategorized" island?
4. WebGL2 fallback quality — accept degraded shader effects, or detect and show a "WebGPU recommended" banner?

## Sign-off gate

Before proceeding to Phase 1:

- [x] Owner signs off on metaphor — **Harmonic Nebula** (confirmed 2026-04-20)
- [ ] Owner resolves open questions 1-4
- [ ] User-facing copy list approved ("nebular clouds", "grips", "similar sounds", "rare voicings")
