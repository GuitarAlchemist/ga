---
name: Karpathy Principles
description: Strict coding guidelines prioritizing simplicity, surgical changes, and goal-driven execution based on Andrej Karpathy's philosophy.
---

# Karpathy-Inspired Coding Guidelines

When this skill is active, strictly adhere to the following four principles.

## 1. Think Before Coding
**Don't assume. Don't hide confusion. Surface tradeoffs.**

- **State assumptions explicitly**: If uncertain, ask rather than guess.
- **Present multiple interpretations**: Don't pick silently when ambiguity exists.
- **Push back when warranted**: If a simpler approach exists, say so.
- **Stop when confused**: Name what's unclear and ask for clarification.

## 2. Simplicity First
**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If 200 lines could be 50, rewrite it.
- **The Test**: Would a senior engineer say this is overcomplicated? If yes, simplify.

## 3. Surgical Changes
**Touch only what you must. Clean up only your own mess.**

- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it — don't delete it.
- **Orphans**: Remove imports/variables/functions that YOUR changes made unused. Don't remove pre-existing dead code unless asked.
- **The Test**: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution
**Define success criteria. Loop until verified.**

Transform imperative tasks into verifiable goals.

- Instead of "Add validation", use "Write tests for invalid inputs, then make them pass".
- Instead of "Fix the bug", use "Write a test that reproduces it, then make it pass".
- Instead of "Refactor X", use "Ensure tests pass before and after".

For multi-step tasks, state a brief plan:
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
