---
title: Methodology borrows from gstack (QA, security, product office-hours)
status: living
date: 2026-05-14
provenance: Distilled from gstack v1.1+ skill bodies — https://github.com/garrytan/gstack
related:
  - docs/runbooks/chatbot-improvement-loop.md
  - docs/plans/2026-05-07-chatbot-roadmap.md
---

# Methodology borrows from gstack

This runbook captures three opinionated workflows from [Garry Tan's gstack](https://github.com/garrytan/gstack) without installing the gstack runtime. Each section: when to invoke, what good looks like, the checklist, and how it composes with our existing stack (`compound-engineering`, `octo`, in-house tooling).

**Adoption status**: Phase 1 (methodology only). No gstack runtime installed. See `docs/runbooks/chatbot-improvement-loop.md` for the rollout plan.

---

## 1. Real-browser QA (gstack `/qa`)

### When to invoke
- Before declaring any UI / demo / showcase feature done
- After a PR touches `Apps/GaChatbot.Api/wwwroot/`, `Apps/ga-client/src/`, or any user-visible HTML/CSS/JS
- After a "the demo is broken" report from the user

This pairs with memory `feedback_ui_click_through_before_done` (PR #210 shipped 8/15 broken prompts past green CI on 2026-05-12 because corpus tests didn't click). Today's session caught the SSE multi-line writer bug AND the DiatonicChords routing bug via chrome-devtools click-test that the 50-prompt corpus missed.

### What good looks like
- Every advertised showcase prompt clicked through the real chatbot UI
- Screenshot captured for the "before" state and the "after-action" state of each interactive element
- Visual diff against a known-good reference for layout changes
- Console/network panel inspected for errors
- A bug-evidence artifact for each finding: URL + reproduction steps + screenshot + before/after

### Checklist
1. **Pre-test**: deployment URL up? `curl -I` to confirm 200 + correct host.
2. **Open the page** via `mcp__plugin_chrome-devtools-mcp_chrome-devtools__new_page` (or playwright equivalent). Capture initial snapshot.
3. **Inventory advertised features** — for a chatbot demo, that's every prompt in `/api/chatbot/demo`. For a static page, every CTA + form + interactive element.
4. **Exercise each feature**:
   - Click the action
   - Wait for the network/render to settle (use `wait_for` on a known-good post-action string)
   - Take snapshot — verify expected text/structure
   - Capture screenshot
   - Inspect console for errors
5. **Three tiers** by depth:
   - **Quick (critical only)**: golden path of every feature works
   - **Standard (+ medium)**: edge cases — empty input, abort/stop, network failure simulation
   - **Exhaustive (+ cosmetic)**: layout overflow, responsive breakpoints, accessibility tree, motion
6. **Score before/after**: count visible bugs at start, count after fixes. Each tier has a pass threshold.
7. **Output**: a markdown report with screenshot evidence per finding, ship-readiness verdict.

### How it composes with what we have
- **chrome-devtools-mcp** and **playwright** plugins are already installed — they're the tooling layer.
- **`compound-engineering:ce-test-browser`** runs browser tests on pages affected by the current PR.
- **What gstack adds**: the *methodology* — what to test, what tier, what evidence to capture, when to call it done. Use this checklist when invoking either tool.
- Run alongside `PromptCorpusTests` (the YAML corpus) for backend coverage. Visual QA + corpus = two distinct safety nets that catch different bug classes.

---

## 2. Security audit — OWASP + STRIDE in one pass (gstack `/cso`)

### When to invoke
- Before merging any PR touching: auth, public-facing endpoints, MCP tools, F# DSL `ga_dsl_eval` closures, YAML/JSON deserialization, file paths derived from user input, regex on user input
- Before deploying a new public host
- After a security advisory in a key dependency

Note: `octo:security` is currently broken on Windows (same PATH-tokenization bug as `octo:review`, per memory `reference_octo_plugin_corruption_2026_05_10`). Use this checklist with the multi-LLM specialist sub-agent pattern that worked on PR #210 today.

### What good looks like
- Every input sink in the diff identified
- OWASP Top 10 categories explicitly addressed (or marked N/A with reason)
- STRIDE per data flow: **S**poofing, **T**ampering, **R**epudiation, **I**nformation disclosure, **D**enial of service, **E**levation of privilege
- A clear remediation list with severity (🔴 critical / 🟡 medium / 🟢 low) and acceptance criteria
- "No new attack surface" when that's the conclusion — explicit, not assumed

### Checklist
1. **Diff inventory**: list every file touched. Tag each as `auth`, `parser`, `network`, `data-store`, `crypto`, `none`.
2. **OWASP Top 10** sweep:
   - A01 Broken Access Control — any new endpoints? any new role/permission paths?
   - A02 Cryptographic Failures — any new keys / hashes / signatures?
   - A03 Injection — any new user-input → SQL / shell / template / regex / LDAP / XPath sinks?
   - A04 Insecure Design — any new trust boundaries crossed?
   - A05 Security Misconfiguration — any new framework defaults exposed?
   - A06 Vulnerable Components — any new dependencies or upgrades?
   - A07 Identification/Auth Failures — any session, MFA, password flow changes?
   - A08 Software & Data Integrity — any new serialization, signing, supply-chain inputs?
   - A09 Logging Failures — any new logs, any sensitive data in logs?
   - A10 SSRF — any user-input → outbound HTTP?
3. **STRIDE** per identified data flow:
   - **S**: can an attacker masquerade as another principal?
   - **T**: can an attacker modify data in transit or at rest?
   - **R**: are operations auditable? non-repudiation?
   - **I**: is sensitive data exposed in errors, headers, logs, network?
   - **D**: are resource bounds enforced? regex DoS? memory amplification?
   - **E**: can authenticated users access privileges they shouldn't?
4. **Output**: one finding per (file, line) with: category, severity, exploitation steps, fix, regression test.

### How it composes
- **`octo:security`** is the closest existing skill — broken on Windows but the prompt body is sound.
- **Specialist sub-agent pattern** that worked today: dispatch `octo:droids:octo-security-auditor` (or `compound-engineering:ce-security-reviewer` if available) with the diff + this checklist as input. Today's PR #210 audit caught 5 LOW findings (0 actionable) using exactly this pattern.
- Pairs with the Demerzel QA Architect tribunal (memory `project_qa_architect_tribunal`) — adopt this checklist as one of the tribunal's named lenses when the contract version bumps.

---

## 3. Product office-hours — six forcing questions (gstack `/office-hours`)

### When to invoke
- Before starting any non-trivial feature (>1 day of work)
- After a "should we build X" question from anyone
- When a `BACKLOG.md` item is about to enter `/feature` / `ce-plan` flow
- Stuck on prioritization between competing features

### What good looks like
- The user (or team) gives crisper, more honest answers to the six questions than they would in a normal scoping call
- A "kill, keep, or shape" verdict emerges — sometimes the answer is "don't build this"
- Subsequent planning starts from real customer signal, not assumption

### The six forcing questions
1. **Who is this for?** (Not "developers" — be specific. Which user, in what context, doing what?)
2. **What pain are they in right now?** (Concrete, in their words. If you can't quote them, you don't know.)
3. **What do they do today instead?** (Workaround, competitor, hack, give up?)
4. **How will this change their day?** (Be brutal. "Saves 5 minutes a week" is different from "unblocks the entire workflow.")
5. **Why us? Why now?** (What changed? What gives you the right to compete here? What window is closing?)
6. **What would make this fail?** (Pre-mortem. Top three failure modes, ranked by likelihood × impact.)

### How to run it
- 30 minutes max per topic
- Write the answers down (in a `docs/plans/YYYY-MM-DD-*.md` if it's a real feature, in chat scratch if it's a discard)
- If any of #1–#4 produce a vague answer → stop. Go talk to a real user before continuing.
- Question #6 is non-negotiable. If you can't name three failure modes, you don't understand the problem.

### How it composes
- **`compound-engineering:ce-brainstorm`** is the closest match — collaborative dialogue to surface scope. Use these six questions as the FIRST pass before invoking ce-brainstorm.
- **`compound-engineering:ce-plan`** comes after — once #1–#6 are answered, planning has real ground truth to build on.
- Pairs with `docs/plans/` doc shape (per CLAUDE.md collaboration discipline): the answers to these six questions become the "Problem (one-way door risks)" section of a plan doc.

---

## Adoption status & next steps

- **Phase 1 (this doc)** — methodology in repo, validate on next non-trivial PR
- **Phase 2** — if validated, integrate `/cso` checklist into Demerzel tribunal verdict contract (after the 2026-05-18 Phase 1 trigger fires, or coordinate a contract version bump)
- **Phase 3** — only if Phase 2 paid off, consider full `./setup` install across GA + Demerzel + ix + tars

Upstream tracking: gstack v1.1.0 as of 2026-05-13. Garry ships fast — if you ever full-install, pin to a SHA in `.claude/skills/gstack/.git/HEAD`, don't auto-update.
