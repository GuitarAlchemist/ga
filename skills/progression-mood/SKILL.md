---
name: "progression-mood"
description: "Reliable techniques for darkening or brightening a chord progression — parallel-mode swaps, modal interchange (Phrygian / Aeolian / Dorian / Lydian / Mixolydian), and borrowed-chord substitutions. Use when a learner asks how to make a progression sound darker / sadder / moodier / brighter / more uplifting."
triggers:
  - "darker"
  - "darken"
  - "moodier"
  - "sadder"
  - "melancholy"
  - "minor sounding"
  - "minor-sounding"
  - "brighter"
  - "brighten"
  - "uplifting"
  - "happier"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "deterministic-catalog"
  origin: "ported from Common/GA.Business.ML/Agents/Skills/ProgressionMoodSkill.cs (PR #71) — second canary for SKILL.md authoring"
  evidence-kinds:
    - catalog_lookup
---

# Progression Mood — Darkening and Brightening Techniques

Use this catalog when a learner asks how to shift the mood of a progression. Pick the **darken** branch when the query mentions darker / sadder / moodier / melancholy / "minor sounding"; pick the **brighten** branch when the query mentions brighter / uplifting / happier. If both intents appear, default to darken (it's the more common ask) and offer brighten as a follow-up.

Reproduce the technique list verbatim — the choice of techniques and their ordering is pedagogically validated.

## Darken branch

Open with: *"Here are five reliable ways to darken a progression. Pick one or stack them — each shifts the harmonic mood without abandoning the underlying key."*

1. **Swap to parallel minor** — replace the tonic's major chord with its parallel minor (C → Cm). Pulls everything that follows toward minor-mode resolution.
2. **Borrow from Aeolian (natural minor)** — substitute IV → iv, vi → bVI, V → v. So `C F G` → `C Fm G` or `C bA G`. Standard pop-ballad move.
3. **Borrow from Phrygian** — drop in bII (Db in C major) or use bII–I as a final resolution. The half-step approach gives a Spanish/cinematic colour.
4. **Borrow from Dorian** — keep the i minor but swap iv → IV (Dorian's natural 6) for the bittersweet, modal-folk feel of *Scarborough Fair*.
5. **Replace V with bVII** — `C bB F` instead of `C G F`. Common in rock; the lack of a leading tone weakens the pull home and reads as moodier.

Close with: *"Combine 1 + 2 for a strong sad-pop transformation; 3 alone for film-score gravity; 4 alone for folk melancholy."*

## Brighten branch

Open with: *"Here are four reliable ways to brighten a progression that feels too dark or static."*

1. **Swap to parallel major** — flip a minor tonic to its parallel major (Am → A). Strongest mood-flip available.
2. **Borrow from Lydian** — raise IV → #IV (#iv° actually), or hold a IV with a #11 colour. Floating, dreamy lift.
3. **Borrow from Mixolydian** — add bVII as a passing chord that doesn't resolve down (`I bVII IV I`); rock-anthem brightness.
4. **Reinforce V → I** — make sure the dominant resolves cleanly back to tonic. Add a V7 or V/V to strengthen pull.

Close with: *"Combine 1 + 4 for a definitive lift from minor to major; 2 alone for ethereal/cinematic brightness."*

## What this skill does NOT do

Do not attempt to transform a specific named progression on the fly (e.g. "darken `C Am F G` for me"). That requires position-by-position rewriting beyond a catalog answer — defer those queries to the LLM agent path with the catalog as background context.

## Cross-reference

- C# implementation: `Common/GA.Business.ML/Agents/Skills/ProgressionMoodSkill.cs`
- Tests: `Tests/Common/GA.Business.ML.Tests/Unit/ProgressionMoodSkillTests.cs`
