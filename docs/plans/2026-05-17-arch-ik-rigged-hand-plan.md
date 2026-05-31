---
title: IK demo — rigged-hand model swap
created: 2026-05-17
owner: spareilleux
status: draft
revisit_trigger: when picking up IK demo work
reversibility: easy (each phase is its own PR)
---

# IK demo — rigged-hand model swap

## Problem

The IK demo at `/test/inverse-kinematics` renders a hand from cylinders + spheres. After PR #257 the wrist orientation is correct (perpendicular to fretboard), but the procedural hand has four real anatomical defects:

1. No thumb (`FINGER_NAMES = [index, middle, ring, pinky]`)
2. Fingertips sit 10mm above strings (target Y = `NECK_THICKNESS/2 + 0.03`, strings at +0.02)
3. Fingers look visually short — bones bunch up under the wrist due to tilt + close wrist position
4. Cylinders & spheres are the wrong abstraction for a realistic "guitar hand"

## What was tried (2026-05-17 session, post-#257) and why it stalled

| Attempt | Branch | Result |
|---|---|---|
| OpenXR `generic-hand` GLB from `@webxr-input-profiles/assets@1.0` CDN | `feat/ik-xrhand-mesh-model` | Loads (25 bones), wrist orientation correct, but rest pose is a tightly-curled fist that reads weird on the fretboard. Per-bone driving from FABRIK not wired. |
| Switch to `left.glb` | same | Same fist-pose issue. |
| Dorchester3D rigged hand (already at `public/Dorchester3D_com_rigged_hand.glb`) | `feat/ik-sketchfab-hand-rig` (stashed) | It's a FULL upper body (59 bones, two arms with shoulders). Wrist not at model origin. Mesh subset visible as `Cube005` (left hand). Reposition + scale calculation didn't render anything visible in attempted iteration. |
| Sketchfab CC-BY "Hand Rig" by CreativeMachine | not attempted | Requires manual download (Sketchfab login). User would place at `public/assets/hand-rig.glb`. |

Root cause of stall: I was iterating on screenshot-polling without an upfront plan. Each issue chained into another (model size → material → rest pose → coordinate frame → mesh hiding → invisible result), and the cycle time per fix is high because I can't see what the user sees directly.

## Plan

### Phase 0 — this document
Status: complete on commit of this file.

### Phase 1 — pick + check in the asset

Decision needed (user): which asset to commit at `ReactComponents/ga-react-components/public/assets/hand-rig.glb`. Constraints:

- glTF/GLB format (Three.js native via `GLTFLoader`)
- Rigged with bones for each phalanx (3 per finger, 2 for thumb)
- Rest pose: relaxed open palm or slight curl, NOT fist
- Compact: just a hand, NOT a full character / arm
- License compatible with CC-BY attribution
- Under 2MB

Concrete candidates worth investigating before picking:
- Sketchfab "Hand Rig" by CreativeMachine (CC-BY, manual download)
- BlenderKit free rigged hands (CC-BY, manual download)
- A hand extracted from Mixamo's character library (free with Adobe account)
- Generate a rest-posed GLB in Blender from a free base mesh (slowest)

Acceptance: file exists at the expected path, `git status` shows it added, file size < 2MB, loads via GLTFLoader without errors.

### Phase 2 — skeleton inspection

In a one-shot inspection PR: load the asset under `__gaIK._debug.handRoot`, log every Bone name + local position + parent name to the console. Capture the bone hierarchy as `docs/reference/ik-asset-skeleton.md`. Decide bone-name → finger-chain mapping.

Acceptance: map of asset bone names → (`thumb-proximal`, `thumb-distal`, `index-proximal`, `index-intermediate`, `index-distal`, …) committed alongside the asset.

### Phase 3 — static render (no IK driving)

Replace the procedural cylinders/spheres with the loaded SkinnedMesh in its rest pose. Tune scale, position offset (so the asset's wrist bone coincides with the wrist Group origin), and material override.

Acceptance:
- Hand renders at fretboard scale (visible span ~3 strings × 4 frets)
- Wrist orientation = perpendicular (`validate().metrics.wristPerpendicularityDeg < 5`)
- No console errors
- Looks recognizably like a hand in a screenshot

This is the ship-or-die phase. If the asset's rest pose can't be made to look like a guitar hand by positioning alone (Sketchfab Hand Rig likely OK; OpenXR fist NOT OK), revisit Phase 1.

### Phase 4 — per-bone driving from FABRIK

For each finger chain, set each phalanx bone's quaternion via `bone.lookAt(nextJointWorld)` after the FABRIK pass. Compute the rest-pose offset matrix per bone so `lookAt` rotates relative to rest, not world `+Y`. Thumb special-case: 2-bone chain.

Acceptance:
- Switching chord visibly moves fingers to new positions
- `validate().issues` empty for all 9 chord library presets

### Phase 5 — introspection bundle

Add to `__gaIK`:
- `captureViews()` → `{ top, front, side, perspective }` base64 PNGs in one call
- `setControls({ showAxes: true })` — render world axis arrows at wrist + finger MCPs for debugging
- Extended `validate()` already in WIP — port forward

Acceptance: each future iteration round-trip needs one MCP call (`captureViews()`), not four.

## Out of scope

- Animation between chord transitions (slerp finger bones across N frames). Phase 4 snaps; smooth transitions are a separate concern.
- Multiple hands / barre fingerings beyond what current FABRIK handles.
- Per-string string-bending visualization.

## Open questions for user

1. Asset preference — Sketchfab Hand Rig vs Mixamo vs Blender-generated vs other?
2. Is per-chord finger animation required (Phase 4), or is static rest pose acceptable as v1?
3. Acceptable build size impact for a checked-in GLB (under 2MB OK?)

## Reversibility

Each phase is its own PR. Phases 1-3 can be reverted by deleting the asset file + a one-file revert. Phases 4-5 are additive on top.

The previously-merged PR #257 (wrist perpendicular fix) stands regardless of model-swap outcome.

## Status of related work

- ✅ PR #257 merged on main — wrist perpendicular to fretboard
- 🟡 `feat/ik-xrhand-mesh-model` — OpenXR attempt, WIP-committed for reference
- 🟡 `feat/ik-sketchfab-hand-rig` — Dorchester3D surgery attempt, stashed (not committed)
- ⬜ this plan doc (Phase 0)
