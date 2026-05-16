---
title: IK joint constraints — per-phalanx anatomical curl
status: planned
date: 2026-05-16
reversibility: two-way door
revisit_trigger: "after #218 ships or if a user complaint specifies the constraint failure mode (current pose is acceptable for the 6/8 clean chord set)"
related:
  - ReactComponents/ga-react-components/src/components/InverseKinematics/InverseKinematics.tsx
  - PR #243 (basis + wrist position fix; left this as follow-up)
  - task #222
  - task #223 (closed — MCP control surface that surfaces the FABRIK convergence numbers)
---

# IK joint constraints — per-phalanx anatomical curl

## Why

PR #243 fixed the "hand crosses the fretboard" bug by repositioning the wrist above the neck and tilting the palm forward. `window.__gaIK.validate()` now reports 6 of 8 chord shapes fully clean (no joint inside neck wood, fingertip on target). The remaining 2 shapes (F barré, Bb barré) show pinky tip 0.18–0.29 units from target — FABRIK cannot find a straight-line pose that reaches the far string from the pinky's MCP given its 0.72 bone budget.

Joint constraints will **NOT** fix the pinky-reach issue (a 0.72-budget pinky cannot reach a 1.0-unit target regardless of constraints; only longer bones or different finger assignment would help). What joint constraints WILL fix:

1. **Intermediate-frame artefacts during chord-change animations** — without DOF limits, FABRIK can flatten the pre-curled pose during slerp transitions, producing momentary hyperextension.
2. **Pose realism in static screenshots** — current curl is visually OK but not strictly anatomical; some bones can extend through ranges a real finger can't.

## Decision: defer minimal-version implementation

A minimal "no-hyperextension" plane projection (applied as a post-FABRIK pass per iteration) was prototyped on 2026-05-16. Result: regressed Bb barré index + middle convergence (0.12 short of target each) because the constraint forbids the straight-line poses those fingers need to reach far frets. The constraint cannot distinguish "natural flat extension" from "hyperextension past flat."

Proper fix requires the full per-DOF model below. That's ~150 lines and an iteration loop of its own — out of scope for the PR #243 session.

## Proposed approach (full per-DOF model)

Apply joint constraints as a **post-FABRIK refinement pass** per iteration, not inside the forward/backward walks. This preserves FABRIK's bone-length guarantees and lets the constraint act as a hard projection.

Per joint:

| Joint | Type | DOF | Constraint |
|---|---|---|---|
| MCP (joints[1]) | Cone | 2 | Flex 0–90°, abduction ±15°. Cone half-angle ~60° around palmForward |
| PIP (joints[2]) | Hinge | 1 | Flex 0–100°. Rotation axis = perpendicular to MCP-PIP bone in the palm-up plane |
| DIP (joints[3]) | Hinge | 1 | Flex 0–100°. Rotation axis = perpendicular to PIP-DIP bone in the palm-up plane |

### Algorithm

```typescript
function refineJointsWithConstraints(
  joints: Vector3[],
  bones: number[],
  palmBasis: { forward, up, right },
): void {
  // i = 1 (MCP): project onto cone around palmBasis.forward
  const mcpDir = joints[1].clone().sub(joints[0]).normalize();
  const cosAngle = mcpDir.dot(palmBasis.forward);
  if (cosAngle < Math.cos(coneHalfAngle)) {
    // Rotate mcpDir back toward palmBasis.forward until on cone surface
    const axis = palmBasis.forward.clone().cross(mcpDir).normalize();
    const targetAngle = coneHalfAngle;
    const rot = new Quaternion().setFromAxisAngle(axis, targetAngle);
    mcpDir.copy(palmBasis.forward).applyQuaternion(rot);
    joints[1].copy(joints[0]).addScaledVector(mcpDir, bones[0]);
  }

  // i = 2 (PIP): hinge around (parent bone × palmBasis.up).normalize()
  // i = 3 (DIP): same pattern for PIP-DIP bone
  // For each: compute current rotation around hinge axis;
  // clamp to [0, 100°]; reconstruct joint position.
}
```

Insert after the backward pass inside `fabrikSolve`:

```typescript
for (let it = 0; it < iterations; it++) {
  // forward pass
  // backward pass
  refineJointsWithConstraints(joints, bones, palmBasis);
}
```

### Estimated work

- `projectJointToCone`: ~50 lines
- `projectJointToHinge`: ~40 lines
- `refineJointsWithConstraints`: ~60 lines
- Integration into `fabrikSolve` + plumbing the palm basis through: ~10 lines
- **Total: ~150-180 lines + ~30 lines of unit-test-style validation via `window.__gaIK.validate()`**

## Success criteria

- `validate()` returns ≤ 2 issues across all 8 chord shapes (same as today for wood-piercing; allow up to 2 barré convergence misses on pinky)
- Visual: hand pose during slerp chord transitions stays continuously curl-shaped (no hyperextension frames)
- No regression on the 6 currently-clean chord shapes
- ChordChange animation feels smooth (visual judgement)

## Out of scope

- Pinky-reach for 4-finger barré chords (different fix: longer bones, or finger-reassignment, or accept the visual)
- Thumb modeling (current hand has no thumb)
- Two-handed playback (not part of demo)
