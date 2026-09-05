---
id: 2026-08-02-wayfinder-reconciliation-cadence
date: 2026-08-02
status: concluded
domain: code
question: Should GA run periodic Skills Wayfinder sessions to reconcile local sessions, plans, and issue state?
hypotheses:
  - claim: Wayfinder should be triggered by a large foggy destination, not by a calendar; periodic reconciliation should only test whether that trigger has appeared.
    refuted_if: The primary Wayfinder skill or its first-party documentation defines recurring audits, backlog reconciliation, or calendar cadence as a supported use.
tools: [official-skill-source, official-docs, official-changelog, local-tracker-audit]
artifacts: null
validators: [primary-source-source-doc-changelog-triangulation]
confidence: high
supersedes: null
superseded_by: null
---

# Wayfinder as a reconciliation cadence

## TL;DR

**Do not schedule periodic `/wayfinder` sessions.** Wayfinder is a user-invoked,
upstream planning workflow for one destination that is both larger than one agent
session and still too foggy to spec. It creates a durable issue map and works one
decision ticket per session until the route is clear. Re-running it on a calendar
would duplicate maps/tickets, turn a situational on-ramp into a standing process,
and blur the source of truth with the existing Cloud Factory program and plan.

Run a cheap **Wayfinder eligibility check** at a program gate or monthly triage:
if a materially new destination is too large for one session and the route cannot
yet be expressed as a spec or tracer-bullet tickets, invoke Wayfinder once and
continue that map until its frontier is empty. Otherwise use ordinary issue/PR
triage, evidence freshness, Gaia collision/base-rate checks, and the Cloud Factory
packet reconciliation flow.

For the current Cloud Factory effort, the destination, plan, linked contracts,
authority boundaries, and first tracer are already explicit. Starting a second
Wayfinder map now would duplicate the existing program rather than clear fog.

## Question and hypothesis

The pain is real: local Claude/Codex sessions, GitHub issues/PRs, sibling-repo
contracts, and dirty worktrees drift independently. The question is whether a
recurring Wayfinder session is the right reconciliation mechanism.

Hypothesis: **no**. Wayfinder should be invoked on an evidence-based fog trigger;
the recurring control should only detect that trigger. This would be refuted if
the primary skill defined periodic audit/reconciliation as part of its job.

## Reproducible method

Source snapshot: `mattpocock/skills@2ab958093e83e0ec752e6c1c5932da465bf23e0c`.

```powershell
git ls-remote https://github.com/mattpocock/skills.git refs/heads/main
gh api repos/mattpocock/skills/contents/skills/engineering/wayfinder/SKILL.md `
  --jq .download_url
Get-Content docs/agents/issue-tracker.md
rg -n "wayfinder|Wayfinding operations" CLAUDE.md AGENTS.md docs .claude/skills
```

I compared the canonical skill, its first-party human documentation and release
changelog, then checked whether this GA branch has the tracker operations the
skill expects.

## Evidence

1. The canonical skill describes Wayfinder as planning a **huge, foggy effort**
   toward one named destination; it produces decisions, not build deliverables.
   It is explicitly user-invoked (`disable-model-invocation: true`). Each map is
   one `wayfinder:map` issue and each ticket is a one-session question. The map is
   finished when no decisions remain before implementation
   ([source, pinned](https://github.com/mattpocock/skills/blob/2ab958093e83e0ec752e6c1c5932da465bf23e0c/skills/engineering/wayfinder/SKILL.md)).
2. The first-party guide says to use it when an effort exceeds one session and is
   too foggy to spec; if the opening grill finds no fog, it stops rather than
   creating a map. Once clear, it hands off to the spec/ticket/build flow
   ([Wayfinder guide](https://www.aihero.dev/skills-wayfinder)).
3. The changelog deliberately calls Wayfinder a **situational on-ramp, not the
   default spine**, and records the no-fog early exit. It also requires native
   tracker blocking/frontier operations configured by the setup skill
   ([changelog, pinned](https://github.com/mattpocock/skills/blob/2ab958093e83e0ec752e6c1c5932da465bf23e0c/CHANGELOG.md#L256-L269)).
4. This GA branch's `docs/agents/issue-tracker.md` has ordinary GitHub operations
   but no `Wayfinding operations` section, and no Wayfinder skill is currently
   installed project-scoped. A scheduled invocation would therefore fall back or
   improvise tracker semantics instead of using the intended shared frontier.
5. The current Cloud Factory reconciliation already has a named destination, a
   committed plan, explicit authority ladder, linked program/issues/contracts,
   and a first tracer. That is downstream of the fog Wayfinder is designed to
   clear.

## Verdict

### Adopt the trigger, not a periodic Wayfinder run

At each monthly issue/PR reconciliation or major program gate, answer four cheap
questions:

1. Is there a **new or materially redrawn destination**?
2. Is it clearly larger than one agent session?
3. Is the route still too foggy to write a coherent spec or tracer-bullet ticket
   frontier?
4. Would a new map have one canonical home rather than duplicate an existing
   program, plan, or issue tree?

Invoke Wayfinder only when all four are true. Continue the same map one frontier
ticket per session; do not create a fresh map next month. When no decisions remain,
hand off to spec/tickets and stop Wayfinder for that destination.

### Current decision

- **Cloud Factory / local-session reconciliation:** no new Wayfinder map now.
  Continue the existing plan and GitHub program; use research/prototype tickets
  only when a concrete decision blocks a tracer.
- **Periodic control:** monthly or gate-triggered eligibility check folded into
  ordinary triage, not an autonomous planning session.
- **Prerequisite before first real use:** update/install the current Matt Pocock
  skills and let its setup add the canonical `Wayfinding operations` section to
  the tracker docs. Review that diff before creating a map.
- **Automation:** do not schedule one yet. A future automation may report
  `wayfinder_trigger=true/false` with evidence, but must not create maps, tickets,
  or decisions without an explicit destination and human start.

## Revisit trigger

Revisit if the Cloud Factory destination changes materially, if three consecutive
reconciliation cycles produce unresolved architecture questions without a clear
frontier, or if upstream Wayfinder gains an explicit audit/reconciliation mode.
