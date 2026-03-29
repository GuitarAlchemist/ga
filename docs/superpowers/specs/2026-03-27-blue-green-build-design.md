# Blue/Green Binary Swap System

**Date:** 2026-03-27
**Status:** Approved
**Scope:** Executable .NET projects (GaApi + microservices)

## Problem

Windows Smart App Control (SAC) blocks newly compiled DLLs that lack reputation. When `dotnet build` overwrites existing trusted binaries, the server cannot start until the new files gain SAC trust. This creates a dead zone where no version of the server can run — the old trusted binaries are gone, and the new ones are blocked.

## Solution

A blue/green binary slot system using NTFS junction points. Two physical build output directories coexist. The server always runs from a junction that points to the active slot. Builds target the inactive slot. Swapping moves the junction.

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Swap mechanism | NTFS junction point | Atomic (~1ms), no file copying, works with any launch method |
| Scope | Executable projects only | GaApi output contains all transitive deps (~235 DLLs). Common/lib projects don't need junctions — their output is copied into the executable project's bin. |
| Dev trigger | Automatic | Build finishes + health check passes → swap |
| Release trigger | Manual | `ga-swap` command with confirmation |
| Trusted baseline | Snapshot + git tag | Pre-build snapshot prevents loss; git tags mark which commit was last trusted (recovery requires VS rebuild or Docker) |
| Slot tracking | `.slot-target` (plain text) + `.slot-state.json` (metadata) | MSBuild reads the simple text file. PowerShell reads JSON. Each tool uses the format it handles well. |

## Directory Layout

```
ga/
├── Apps/ga-server/GaApi/
│   └── bin/
│       ├── blue/net10.0/          ← physical dir, ~235 files (all transitive deps)
│       ├── green/net10.0/         ← physical dir, ~235 files
│       └── active → blue|green   ← NTFS junction point
├── Common/GA.*/
│   └── bin/Debug/                 ← normal build output (no blue/green needed)
├── Scripts/
│   ├── ga-build.ps1               ← builds to inactive slot
│   ├── ga-swap.ps1                ← moves junction, restarts server
│   ├── ga-start.ps1               ← starts from active slot
│   ├── ga-rollback.ps1            ← emergency swap to other slot
│   └── ga-status.ps1              ← shows slot health
├── Directory.Build.props           ← OutputPath override for executable projects
├── .slot-target                    ← plain text: "blue" or "green" (build target)
├── .slot-state.json                ← full metadata (timestamps, health, commits)
└── .gitignore                      ← includes .slot-target, .slot-state.json
```

**Note:** `.slot-target` and `.slot-state.json` are gitignored (local-only). Each developer's slot state is independent.

## Slot State Schema

**.slot-target** (plain text, one word):
```
green
```

**.slot-state.json** (metadata for scripts):
```json
{
  "activeSlot": "blue",
  "lastSwap": "2026-03-27T14:30:00Z",
  "lastBuild": "2026-03-27T15:00:00Z",
  "buildTarget": "green",
  "trustedBaseline": "v1.0-trusted-2026-03-27",
  "slots": {
    "blue": {
      "builtAt": "2026-03-27T12:00:00Z",
      "healthy": true,
      "commitHash": "2b29650"
    },
    "green": {
      "builtAt": "2026-03-27T15:00:00Z",
      "healthy": null,
      "commitHash": "abc1234"
    }
  }
}
```

## Components

### 1. Directory.Build.props — OutputPath Override

Reads `.slot-target` (plain text file) at the solution root. Uses explicit opt-in via `<UseBlueGreenSlots>true</UseBlueGreenSlots>` in each target project's csproj, because:
- Web SDK projects (`Sdk="Microsoft.NET.Sdk.Web"`) don't set `OutputType=Exe` during props evaluation
- Auto-detection via OutputType would incorrectly match AppHost, benchmarks, tools
- Explicit opt-in makes the scope clear and avoids SDK-internal timing issues

Uses `$(MSBuildThisFileDirectory)` for reliable path resolution regardless of how the build is invoked.

**In Directory.Build.props:**
```xml
<!-- Blue/Green build slot support (opt-in per project) -->
<PropertyGroup Condition="'$(UseBlueGreenSlots)' == 'true' AND Exists('$(MSBuildThisFileDirectory).slot-target')">
  <_SlotTarget>$([System.IO.File]::ReadAllText('$(MSBuildThisFileDirectory).slot-target').Trim())</_SlotTarget>
  <OutputPath>bin\$(_SlotTarget)\</OutputPath>
</PropertyGroup>
```

**In each target project's .csproj (e.g., GaApi.csproj):**
```xml
<PropertyGroup>
  <UseBlueGreenSlots>true</UseBlueGreenSlots>
</PropertyGroup>
```

This means:
- `dotnet build` on GaApi writes to `bin/green/net10.0/` (or blue, per .slot-target)
- Common library projects build normally to `bin/Debug/net10.0/`
- Source generators, test projects, AppHost are unaffected
- If `.slot-target` doesn't exist, no override — normal build behavior
- `.slot-target` writes use atomic temp-file + rename to prevent race conditions

### 2. ga-build.ps1 — Build to Inactive Slot

```
1. Read .slot-state.json → determine inactive slot
2. Write inactive slot name to .slot-target
3. Run dotnet build --configuration Debug (OutputPath auto-set by Directory.Build.props)
4. Update .slot-state.json with build timestamp and commit hash
5. Run health check: start server from inactive slot with 30s timeout
   - Kill health check process after check completes (prevent file locks)
6. If health check passes AND trigger=auto → call ga-swap.ps1
7. If health check fails → log warning, do not swap, active slot unaffected
```

### 3. ga-start.ps1 — Start from Active Slot

```
1. Read .slot-state.json → get active slot
2. If no junction exists → detect first run, suggest ga-bootstrap.ps1
3. Verify junction bin/active → correct slot
4. Start server: dotnet bin/active/net10.0/GaApi.dll
5. Wait for /api/health/ping to respond (30s timeout)
6. Report status
```

### 4. ga-swap.ps1 — Move Junction

```
1. Read .slot-state.json → get active and inactive slots
2. Stop running GaApi process (graceful shutdown with 10s timeout, then force kill)
3. Remove junction: cmd /c rmdir bin\active
   (IMPORTANT: never use Remove-Item on junctions — it deletes target contents)
4. Re-create junction: cmd /c mklink /J bin\active bin\{inactive}
5. Update .slot-state.json (flip activeSlot/buildTarget, record timestamp)
6. Update .slot-target to new build target
7. Start server from new active slot
8. Health check: hit /api/health/ping within 30s
9. If health check fails → automatic rollback (swap back to previous slot)
```

### 5. ga-rollback.ps1 — Emergency Swap

```
1. Stop server
2. Swap junction to the other slot (opposite of current active)
3. Update .slot-state.json and .slot-target
4. Start server
5. If both slots fail → report error, suggest VS rebuild or Docker workaround
```

### 6. ga-status.ps1 — Dashboard

```
1. Read .slot-state.json
2. Show: active slot, last build, last swap, commit hashes
3. Check if server is running (GET /api/health/ping)
4. Show DLL timestamps per slot (age = SAC trust proxy)
5. Report which slot the next build will target
```

### 7. ga-bootstrap.ps1 — First-Time Setup

```
1. Test junction creation (create + remove temp junction) — fail fast if not supported
2. Create bin/blue/net10.0/ and bin/green/net10.0/ for GaApi
3. If trusted binaries exist in bin/Debug/net10.0/:
   - Copy to bin/blue/net10.0/
4. Create junction: cmd /c mklink /J bin\active bin\blue
5. Create .slot-target with "green" (next build targets green)
6. Create .slot-state.json with blue as active
7. Tag current commit: git tag trusted-baseline-YYYY-MM-DD
8. Report status
```

## Aspire Integration

The Aspire AppHost (`AllProjects.AppHost`) launches GaApi via `AddProject()` which invokes `dotnet run` on the csproj. This means:

- **When using Aspire:** The OutputPath override in Directory.Build.props applies. Aspire will build and run from the slot specified in `.slot-target`. This works for development but doesn't use the junction — Aspire manages its own process lifecycle.
- **When using ga-start.ps1:** The server runs directly from `bin/active/` via the junction. This is the blue/green-aware path.
- **Recommended dev flow:** Use `ga-start.ps1` for the GaApi server. Use Aspire for infrastructure services (MongoDB, Redis, FalkorDB) that don't have the SAC problem.

## Swap Sequence Diagram

```
Developer runs: ga-build

  ┌─────────────┐     ┌──────────────┐     ┌────────────┐
  │ Read slot    │────>│ dotnet build │────>│ Health     │
  │ state        │     │ → inactive   │     │ check      │
  └─────────────┘     └──────────────┘     └────────────┘
                                                  │
                                          pass ───┤──── fail
                                          │              │
                                    ┌─────▼─────┐  ┌────▼────┐
                                    │ ga-swap   │  │ Log     │
                                    │ (auto)    │  │ warning │
                                    └───────────┘  └─────────┘
                                          │
                              ┌───────────▼───────────┐
                              │ Stop → Move junction  │
                              │ → Start → Health check│
                              └───────────────────────┘
                                          │
                                  pass ───┤──── fail
                                  │              │
                            ┌─────▼─────┐  ┌────▼──────┐
                            │ Running   │  │ Rollback  │
                            │ on new    │  │ to old    │
                            │ slot      │  │ slot      │
                            └───────────┘  └───────────┘
```

## Git Tag Baseline

Periodically (or after confirming both slots work):

```bash
git tag trusted-baseline-$(date +%Y-%m-%d) HEAD
```

**Important:** The git tag marks which commit was last trusted — it does NOT preserve trusted binaries. Recovery from both-slots-broken requires rebuilding from a SAC-whitelisted context:
- Build from Visual Studio (whitelisted by SAC)
- Run the containerized version via Docker as a temporary workaround
- Wait for newly built binaries to gain SAC trust (~hours)

## Bootstrap (First-Time Setup)

Run `ga-bootstrap.ps1` which:
1. Tests junction creation capability (fail fast)
2. Creates blue/green directories for GaApi
3. Copies current trusted binaries to blue slot
4. Creates junction and state files
5. Tags commit as trusted baseline

Since the current trusted binaries were lost (overwritten by today's rebuild), bootstrap will need to either:
- Build from Visual Studio (which is SAC-whitelisted)
- Start the server from Docker as a temporary workaround
- Wait for current binaries to gain SAC trust

## Error Handling

| Scenario | Response |
|----------|----------|
| Build fails | Inactive slot untouched, active slot keeps running |
| SAC blocks inactive slot | Don't swap, log warning, active slot keeps running |
| Health check fails after swap | Automatic rollback to previous slot |
| Health check process hangs | Kill after 30s timeout, prevent file locks on inactive slot |
| Both slots unhealthy | Report error, suggest VS rebuild or Docker |
| Junction creation fails | Fail fast with clear message about permissions |
| .slot-target missing | No OutputPath override — normal build behavior (graceful degradation) |
| .slot-state.json missing/corrupt | Regenerate from directory state (which slot has newer DLLs) |
| Concurrent builds | Unsupported — document that only one `ga-build` should run at a time |

## Testing Plan

1. **Unit:** Slot state JSON read/write, junction creation/removal, .slot-target read
2. **Integration:** Full build → swap → health check → rollback cycle
3. **SAC simulation:** Rename a DLL to trigger load failure, verify rollback works
4. **Concurrent:** Build while server is running, verify no file locks on inactive slot
5. **Bootstrap:** Fresh clone → bootstrap script → working blue/green setup
6. **Graceful degradation:** Delete .slot-target, verify normal build still works

## Governance Integration

The blue/green system maps directly onto Demerzel's governance primitives, making build health a first-class citizen of the governance graph.

### Algedonic Signals

Build events emit algedonic signals that bypass intermediate governance layers:

| Event | Signal Type | Severity | Action |
|-------|------------|----------|--------|
| SAC blocks new build | pain | warning | Log, don't swap, active slot unaffected |
| Health check fails after swap | pain | critical | Automatic rollback |
| Both slots unhealthy | pain | emergency | Alert human, suggest VS rebuild |
| Successful swap | pleasure | info | Record, update belief state |
| Build + swap + health all pass | pleasure | info | Compound metric improvement |

Signal format follows existing `AlgedonicSignalDto`:
- `signal`: `build_slot_health`
- `source`: `ga`
- `node_id`: `build-slot-blue` or `build-slot-green`

### Belief States (Hexavalent Logic)

Each build slot carries a belief: **"This slot is safe to run."**

| Slot State | Truth Value | Confidence | Action |
|------------|------------|------------|--------|
| Just built, untested | U (Unknown) | 0.0 | Gather evidence via health check |
| Health check passes | P (Probable) | 0.7 | Auto-swap in dev mode |
| Running stable for >1h | T (True) | 0.9 | Proceed autonomously |
| Health check fails | D (Doubtful) | 0.3 | Hold, investigate |
| SAC blocks DLL load | F (False) | 0.95 | Do not use this slot |
| Health passes but latency spikes | C (Contradictory) | 0.5 | Escalate, gather more evidence |

Evidence is recorded per belief:
```json
{
  "id": "belief-slot-green-health",
  "proposition": "Green slot is safe to run",
  "truth_value": "P",
  "confidence": 0.7,
  "evidence": {
    "supporting": [{ "source": "health-check", "claim": "/api/health/ping returned 200", "reliability": 0.9 }],
    "contradicting": []
  }
}
```

### IXQL Queries

Build slot nodes become queryable in the Prime Radiant:

```ixql
-- Highlight unhealthy slots
SELECT nodes WHERE type = 'build-slot' AND health.resilience < 0.5 SET glow = true, color = '#FF4444'

-- Pulse the active slot
SELECT nodes WHERE name ~ 'active' AND domain = 'deployment' SET pulse = true, speed = 0.5

-- Show swap edges
SELECT edges WHERE type = 'slot-swap' SET visible = true, color = '#60A5FA'
```

### Governance Graph Nodes

The build system registers as governance nodes:

| Node ID | Type | Health Mapping |
|---------|------|---------------|
| `build-slot-blue` | `build-slot` | resilience = belief confidence, staleness = time since last build |
| `build-slot-green` | `build-slot` | same |
| `build-junction` | `deployment` | resilience = 1.0 if pointing to healthy slot, 0.0 if both broken |

Edges:
- `build-slot-blue → build-junction` (type: `slot-swap`, weight: 1.0 if active, 0.0 if not)
- `build-slot-green → build-junction` (type: `slot-swap`, weight: inverse)
- `build-junction → policy-algedonic-channel` (type: `signal-emitter`)

### Integration Flow

```
ga-build / ga-swap (observable event)
    ↓
Belief State Update (U→P→T or U→D→F)
    ↓
Algedonic Signal (pain or pleasure)
    ↓
Governance Graph Update (node health, edge weights)
    ↓
IXQL Auto-Query (highlight affected nodes in Prime Radiant)
    ↓
Human or Automated Decision (proceed, escalate, rollback)
```

## Future Enhancements

- **VS Code task integration** — `ga-build` as a VS Code task with problem matcher
- **Full Aspire integration** — slot-aware AppHost that uses junctions natively
- **File watcher** — auto-build on save (like `dotnet watch` but slot-aware)
- **SAC trust monitor** — background job that periodically tries to start from each slot, updates health
- **CI pipeline** — GitHub Actions builds to a "release" slot with signed binaries
- **Concurrent build protection** — `.slot-lock` file with PID for multi-terminal safety
