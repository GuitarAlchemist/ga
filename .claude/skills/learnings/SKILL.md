---
name: "Learnings"
description: "End-of-session compound learning capture. Identifies up to 3 surprising/non-obvious things that came out of the session, writes each as a docs/solutions/<category>/<date>-<topic>.md entry with the standard frontmatter so the next session can grep it."
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
last_verified: 2026-05-10
---

# /learnings

End-of-session **compound learning capture**. Mirrors the docs/solutions
ritual that Boris Cherny popularised at Every — every non-trivial
session writes 0–3 entries that the *next* session can find by grep.

## When to Run

- At the end of any session that involved real debugging, design
  decisions, or surprises (i.e., basically every session).
- *Not* on typo / one-line / cosmetic sessions. Skip then.
- *Not* on routine "rebase + push" maintenance unless something broke.

## The Iron Law

```
ONLY write a learning when something was SURPRISING or NON-OBVIOUS.
NEVER duplicate a learning that already exists in docs/solutions/.
NEVER write a "what I did" diary — that's the PR description's job.
A learning answers "what would have saved me 30 minutes today?"
```

If nothing in the session meets that bar, **the right answer is to
write zero entries**. Empty output is allowed and expected.

## Process

### Step 1: Scan the session

Walk the conversation. Pull out anything that fits one of:

- **Bug archaeology** — root cause was non-obvious; future-me would
  benefit from the writeup. (e.g., "p.r vs part.r refactor leftover
  was throwing ReferenceError because the loop variable was renamed
  in only half the file.")
- **Workflow gotcha** — a tool/process behaviour that wasted time and
  is recoverable from. (e.g., "Vite dev server tunnels straight to
  cloudflared — committing fixes the deploy, no separate build step.")
- **Architectural decision with a rationale** — picked X over Y, the
  reason isn't in the code. (e.g., "FABRIK over CCD because chains
  are short and we need preserved bone lengths exactly.")
- **Cross-repo or contract subtlety** — something that touches GA + ix
  / Demerzel / tars and the linkage isn't documented elsewhere.
- **API quirk** — third-party service or library with surprising
  behaviour. (e.g., "GraphQL `metadata` returns numbers not strings
  despite TS type saying Record<string,string>.")
- **Performance / scaling surprise** — a path that's slower or faster
  than expected, with the why.

Skip:

- "I added feature X" — that's git log.
- "Fixed bug Y" without a non-obvious cause — that's git log.
- "User asked for Z" — that's the PR description.
- Anything already documented in `docs/solutions/`, `docs/plans/`, or
  the project memory at `.claude/projects/.../memory/`.

### Step 2: Pick categories

Solutions live in subdirectories by problem type:

```
docs/solutions/
├── architecture/        # composition, layering, abstractions
├── best-practices/      # do/don't patterns
├── compound-reviews/    # multi-LLM review summaries
├── integration-issues/  # cross-repo / external API quirks
├── reviews/             # standalone code review notes
├── runtime-errors/      # crashes / 500s / ReferenceErrors
└── tooling/             # dev environment / build / CI
```

Pick the directory that matches the *kind* of surprise, not the area
of code touched. A React build break caused by a Babel parser oddity
goes in `tooling/`, not `architecture/`.

### Step 3: Write each entry

Filename: `docs/solutions/<category>/<YYYY-MM-DD>-<short-topic>.md`.
Use today's date and a kebab-case topic 3–6 words long.

Frontmatter (mandatory — used by `ce-learnings-researcher` to grep
relevance):

```yaml
---
title: "One-line summary that future-you can scan"
date: 2026-05-10
problem_type: "architecture"   # or runtime-errors / tooling / etc — match the dir
component: "GA.Business.ML.Agents / OPTIC-K corpus"   # the system layer affected
symptoms:
  - "Concrete observable thing"
  - "Another observable thing"
tags: [orchestrator, mcp, dsl-eval]   # optional, helps cross-search
---
```

Body — keep it under ~300 words. Sections:

1. **Symptom** — what looked broken, including the misleading first
   theory if there was one.
2. **Root cause** — the actual underlying reason, with file:line refs
   when relevant.
3. **Fix** — the change that resolved it. Code blocks fine.
4. **Lesson** — one paragraph. Why is this worth remembering?
   What's the rule for next time?

### Step 4: Update MEMORY.md if appropriate

If the learning rises to the level of "this changes how I'd approach
the next session" — not just "this fact is worth knowing" — also add
or update an entry in the user's MEMORY.md so it loads automatically
into future sessions. Use the existing memory format
(see `~/.claude/projects/.../memory/MEMORY.md`).

Most learnings stay in `docs/solutions/` only. Memory is for *behavioural*
guidance (do/don't this); solutions are for *factual* recall ("when X
breaks, look at Y").

### Step 5: Mention in the session-end summary

When closing the session, end with a single line listing the learnings
written, e.g.:

```
Learnings captured: docs/solutions/runtime-errors/2026-05-10-stale-loop-variable-after-refactor.md
```

That tells the user what was preserved without having to read the
files.

## Anti-Patterns

- **Diary entries**: "Today I built the FluffyAnimals component and
  added stripes to the tiger." → not a learning. The PR description
  covers it.
- **Restating CLAUDE.md**: "C# 14 with file-scoped namespaces" → already
  in CLAUDE.md. Skip.
- **Vague platitudes**: "Be careful with state management" → not actionable.
  Either name a concrete bug + fix, or skip.
- **Duplicate of existing entry**: search `docs/solutions/` first; if a
  learning is the same shape as an existing one, *update* the existing
  file instead of creating a new one.

## Output

The skill should report:

- N learnings captured (0 is OK), with paths.
- Whether any MEMORY.md entries were added/updated.
- A one-sentence rationale per skipped candidate (so the user can
  override if they think something's worth keeping).
