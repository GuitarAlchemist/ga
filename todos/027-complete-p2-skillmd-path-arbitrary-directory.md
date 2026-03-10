---
status: complete
priority: p2
issue_id: "027"
tags: [security, skillmd, path-traversal, code-review]
dependencies: []
---

# 027 — `SKILLMD_SKILLS_PATH` Env Var Allows Loading from Arbitrary Directory

## Problem Statement

`SkillMdPlugin.ResolveSkillsPath()` accepts an arbitrary filesystem path from the `SKILLMD_SKILLS_PATH` environment variable with no validation. In compromised container environments or misconfigured deployments, an attacker who can set this env var could point the skill loader at a malicious SKILL.md file that injects arbitrary system prompts into every chatbot request.

## Findings

`Common/GA.Business.ML/Agents/Plugins/SkillMdPlugin.cs` lines 62–66:
```csharp
var env = Environment.GetEnvironmentVariable("SKILLMD_SKILLS_PATH");
if (!string.IsNullOrWhiteSpace(env))
    return env;
```

No validation that:
- The path is within the application directory
- The path is not a network share (`\\server\share\`)
- The path does not traverse above the repo root (`../../etc/`)
- The path exists (handled later in `Register`, but silently returns 0 skills)

Combined with `SkillMdDrivenSkill` using the SKILL.md body directly as the Claude system prompt, a malicious SKILL.md becomes arbitrary prompt injection at the infrastructure level.

## Proposed Solutions

### Option A — Restrict to subdirectory of AppContext.BaseDirectory or repo root
```csharp
var env = Environment.GetEnvironmentVariable("SKILLMD_SKILLS_PATH");
if (!string.IsNullOrWhiteSpace(env))
{
    var resolved = Path.GetFullPath(env);
    var repoRoot = FindRepoRoot(AppContext.BaseDirectory) ?? AppContext.BaseDirectory;
    if (resolved.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase))
        return resolved;
    // Log warning: env var path is outside repo root, ignoring
}
```
- **Pros:** Eliminates path traversal outside the repo; env var still useful for CI variations.
- **Effort:** Small.
- **Risk:** Low — may break edge cases where skills live outside repo (document if so).

### Option B — Remove the env var override entirely
Only support the auto-discovery path (crawl up to `.git`, look for `.agent/skills`).
- **Pros:** Simplest; no attack surface.
- **Cons:** Less flexible for containerized deployments with different layouts.
- **Effort:** Trivial.

### Option C — Allowlist of valid base paths
Configure allowed base paths via `IConfiguration` (`SkillMd:AllowedRoots`), reject env var if not within any allowed root.
- **Effort:** Medium.

## Recommended Action
Option A — preserve flexibility while bounding traversal to the repo tree.

## Technical Details

**Affected file:** `Common/GA.Business.ML/Agents/Plugins/SkillMdPlugin.cs` lines 61–66

## Acceptance Criteria

- [ ] `SKILLMD_SKILLS_PATH` value is validated to be within the repo/app directory
- [ ] Paths outside the allowed boundary are rejected with a log warning
- [ ] Existing auto-discovery behaviour is unchanged when env var is not set

## Work Log

- 2026-03-10: Identified during `/ce:review` of PR #8
