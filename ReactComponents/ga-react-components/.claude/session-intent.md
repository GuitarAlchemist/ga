# Session Intent Contract — TSL Migration

**Created:** 2026-04-03
**Goal:** Build — Migrate all ShaderMaterials to TSL NodeMaterials and enable WebGPU renderer
**Knowledge:** Expert — 6 TSL files already built, knows the API
**Clarity:** Clear requirements — 25 shaders identified, renderer swap path understood
**Success Criteria:**
- Working solution: all shaders running on TSL with no visual regressions
- Production-ready: tested, perf-validated, fallback for non-WebGPU browsers
- No regressions: must keep working at every step — no big-bang migration
- Clear plan: solid incremental plan executable across multiple sessions

**Constraints:**
- Must fit architecture: can't break 3d-force-graph integration or React component API
- High stakes visual: Prime Radiant is flagship — regressions unacceptable

**Key Insight:** TSL is renderer-agnostic (auto-compiles to GLSL on WebGL2, WGSL on WebGPU). The 3d-force-graph WebGLRenderer is NOT a blocker for material migration — only for the final renderer swap.

**Boundaries:**
- Scope: PrimeRadiant directory only (7 files with ShaderMaterials)
- Each shader migrated independently, tested before moving to next
- No changes to component public API or 3d-force-graph integration until Phase 2
- Post-processing migration is Phase 2 (tied to renderer swap)
