---
name: correct
description: Self-improvement reflex. When the user corrects an approach ("no, don't do that", "we discussed this before", "stop X"), captures the lesson as a permanent project rule appended to CLAUDE.md so the pattern doesn't repeat in this or future sessions. Cherny called this "the most important loop" in his 2026 Sequoia talk.
allowed-tools: Read, Edit, Bash
last_verified: 2026-05-14
karpathy_rule: R-self-improvement (Cherny's "most important loop")
---

# /correct

Turns a user correction into a permanent rule appended to `CLAUDE.md`.

## When to run

- User says **"no, don't do that"** / **"stop X"** / **"we discussed this before"**.
- User overrides a recommendation with a reason that would apply to future work.
- User points out a recurring pattern you should avoid.

**Do NOT** invoke for typo corrections or one-off style nudges. Bar: would
the correction apply to *future* work, not just this edit?

## How to run

1. **Identify the rule.** One sentence, imperative, with the *why*.
   - Good: "Never amend commits after CI passes — force-push hides the
     original test result."
   - Bad: "Stop amending commits."

2. **Confirm** (one line, skip if user already explicitly said so):
   > Adding rule to CLAUDE.md: <rule>. OK?

3. **Append** to `CLAUDE.md` under `## Session-learned rules` (create the
   section if missing — last section so new entries append). Format:

   ```markdown
   - **<YYYY-MM-DD>**: <rule>. (<one-line reason>)
   ```

4. **Report**:
   > Rule added to CLAUDE.md: <rule>

## Anti-patterns

- Vague rules ("be careful with state management") — name the pattern or skip.
- Over-eager capture — typos aren't rules.
- Code-style catalogs — those go in style files, not CLAUDE.md.
- Confusing with /learnings — /learnings = surprises; /correct = behavioural rules.

## Why this exists

Cherny's "most important loop" from the 2026 Sequoia AI Ascent talk:
when corrected, update the localized machine, not just this turn's code.
Without /correct, the next session repeats the pattern because nothing
persisted the rule.

## Related

- `/digest` — captures session state.
- `/learnings` — captures surprises into `docs/solutions/`.
- `CLAUDE.md` — the persistent rule store this skill writes to.
