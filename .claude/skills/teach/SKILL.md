---
name: teach
description: Personalized AI tutor — turns any topic into a customized, progress-tracked learning course. Use when the user wants to LEARN a concept, skill, language, tool, or domain. Creates a MISSION, curated RESOURCES, iterative lessons with self-checks, and learning-records that persist across sessions. NOT for interrogating the user about their own design (use brainstorming/IDSD for that).
---

# /teach — personalized learning tutor

Adapted from aihero.dev (`learn-anything-with-my-teach-skill`). Turns the agent
into a tutor that builds a personalized course for whatever the user wants to learn,
adapted to their goal and proficiency, with progress tracked across sessions.

## When to use
The user wants to **learn** something. NOT for the agent interrogating the user
about *their* design/plan — that's `brainstorming` / the IDSD intent gate.

## Workflow

### 1. Assess before teaching (ask one concise batch)
- **Motivation** — why learn this, and what will they do with it?
- **Current level** — none / some exposure / intermediate / advanced?
- **Success criteria** — what does "done" look like concretely?
- **Modality** — worked examples, theory-first, exercises, analogies, projects?

> "The more detail you give, the better your personalized lessons will be."

### 2. Set up the course under `./learning/<topic>/`
- `MISSION.md` — goals, current level, success criteria (from step 1).
- `RESOURCES.md` — curated materials. **Ground every entry in a real, verified
  source — never fabricate links or citations** (fetch/verify before listing).
- `reference/glossary.md` — key terms, grown as the course proceeds.
- `lessons/` — one file per lesson (Markdown by default; HTML if embedded
  quizzes/audio are wanted — optional, heavier).
- `learning-records/` — what was covered, what landed, what didn't.

### 3. Deliver lessons iteratively
- One lesson at a time, scoped to the assessed level — short and concrete,
  prefer worked examples over walls of theory.
- End each lesson with a brief self-check (a few questions + answers).
- Stop and wait for the learner to report how it went.

### 4. Adapt
- After each lesson, update `learning-records/` and `reference/glossary.md`.
- Generate the next lesson based on what landed and what was missed.

## Guardrails
- Resources must be real and verified — no invented citations (ecosystem rule).
- Keep lessons tight; one concept at a time; check understanding before advancing.
- Persist progress so a later session can resume from `learning-records/`.
