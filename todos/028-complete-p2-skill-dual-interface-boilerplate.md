---
status: complete
priority: p2
issue_id: "028"
tags: [architecture, code-quality, dry, code-review]
dependencies: []
---

# 028 — Dual `CanHandle`/`ExecuteAsync` Boilerplate in Every Orchestrator Skill

## Problem Statement

Every skill that extends `AgentSkillBase` (which implements `IAgentSkill`) AND implements `IOrchestratorSkill` must write two identical adapter methods. This boilerplate is pure noise and a copy-paste hazard — a future skill author may forget to delegate correctly.

## Findings

Pattern repeated in `ProgressionCompletionSkill`, `KeyIdentificationSkill`, `ChordSubstitutionSkill`, `FretSpanSkill`, `ScaleInfoSkill`:

```csharp
// IAgentSkill adapters — pure boilerplate
public override bool CanHandle(AgentRequest request) => CanHandle(request.Query);

public override Task<AgentResponse> ExecuteAsync(AgentRequest request, CancellationToken ct = default) =>
    ExecuteAsync(request.Query, ct);
```

This exists because:
- `AgentSkillBase` implements `IAgentSkill` (takes `AgentRequest`)
- `IOrchestratorSkill` takes `string` directly
- Each skill inherits both, forcing the adapter

If a skill author accidentally writes `CanHandle(request.Query.ToUpper())` in the adapter, bugs surface only at runtime.

## Proposed Solutions

### Option A — Move adapters to `AgentSkillBase` using abstract `CanHandle(string)` (Recommended)
```csharp
public abstract class AgentSkillBase(...)
{
    // Abstract string-level methods (what skills actually implement)
    public abstract bool CanHandle(string message);
    public abstract Task<AgentResponse> ExecuteAsync(string message, CancellationToken ct = default);

    // IAgentSkill adapters — once, in the base class
    public sealed override bool CanHandle(AgentRequest request) => CanHandle(request.Query);
    public sealed override Task<AgentResponse> ExecuteAsync(AgentRequest request, CancellationToken ct = default) =>
        ExecuteAsync(request.Query, ct);
}
```
Skills only implement `CanHandle(string)` and `ExecuteAsync(string)`. Zero boilerplate.

- **Pros:** Eliminates all 5 pairs of adapter boilerplate; centralises the contract.
- **Cons:** Minor refactor to base class; all skills change signature (mechanical, not risky).
- **Effort:** Small (base class change + remove 10 adapter methods).
- **Risk:** Low — pure refactor, behaviour identical.

### Option B — Separate `IOrchestratorSkill` from `IAgentSkill` entirely
New skills that are orchestrator-only extend a new `OrchestratorSkillBase` (no `AgentRequest`). Old skills remain as-is.

- **Pros:** Clean separation, no forced adapter.
- **Cons:** Two inheritance hierarchies diverge.
- **Effort:** Medium.

### Option C — Accept as-is (do nothing)
Current pattern works and tests cover it.

- **Pros:** No change risk.
- **Cons:** Every new skill will repeat the boilerplate.

## Recommended Action
Option A — pure refactor, no behaviour change, eliminates ~20 lines of boilerplate.

## Technical Details

**Affected files:**
- `Common/GA.Business.ML/Agents/AgentSkillBase.cs` (change)
- `Common/GA.Business.ML/Agents/Skills/ProgressionCompletionSkill.cs` (remove adapters)
- `Common/GA.Business.ML/Agents/Skills/KeyIdentificationSkill.cs` (remove adapters)
- `Common/GA.Business.ML/Agents/Skills/ChordSubstitutionSkill.cs` (remove adapters)
- `Common/GA.Business.ML/Agents/Skills/FretSpanSkill.cs` (remove adapters)
- `Common/GA.Business.ML/Agents/Skills/ScaleInfoSkill.cs` (remove adapters)

## Acceptance Criteria

- [ ] No skill class contains `CanHandle(AgentRequest) => CanHandle(request.Query)`
- [ ] No skill class contains `ExecuteAsync(AgentRequest, ct) => ExecuteAsync(request.Query, ct)`
- [ ] `AgentSkillBase` provides final adapter implementations
- [ ] All existing tests pass unchanged

## Work Log

- 2026-03-10: Identified during `/ce:review` of PR #8
