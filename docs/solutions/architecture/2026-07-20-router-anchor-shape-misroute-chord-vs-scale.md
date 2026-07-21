---
module: GA.Business.ML
tags: [routing, semantic-router, example-prompts, embeddings, bge-large, chatbot, eval-blindness]
problem_type: bug-fix
decision: Fix misroutes by adding ExamplePrompt anchors keyed on the chord-quality NOUN (triad/chord), defended on the scale side, verified by A/B against a rebuilt baseline binary
rejected: [keyword/regex hint rules, offline few-intent cosine simulation as sole verification, editing ScaleInfoSkill's ambiguous anchor away]
reason: The two skills must separate on the trailing noun rather than the "<root> <quality>" prefix they share; and only a rebuilt-binary A/B can see what production's full intent ranking does
date_decided: 2026-07-20
---

# "What notes are in a C major triad" returned the 7-note scale

**Date:** 2026-07-20
**Issue:** [#555](https://github.com/GuitarAlchemist/ga/issues/555) · **Fix:** PR #558
**Status:** fixed, live-verified, corpus-pinned both directions

## Symptom

The product's most basic question answered the wrong thing, confidently:

```
Q: What notes are in a C major triad
A: (skill.scaleinfo @ 0.90) The C major scale has 7 notes: C – D – E – F – G – A – B
```

A 3-note triad was requested; a 7-note scale came back. Two sibling failures:

| prompt | routed to | conf | returned |
|---|---|---|---|
| `What notes are in a G7 chord` | `skill.improvisation` | 0.85 | scale/mode suggestions |
| `What notes are in a C major triad` | `skill.scaleinfo` | 0.90 | the 7-note scale |
| `spell G7` | `skill.chordvoicings` | 0.48 | fretboard diagrams |

Not a low-confidence fallback — a *high-confidence wrong pick*.

## Root cause: anchor shape, not a missing skill

`skills/chord-info/SKILL.md` already declared the literal trigger
`"what notes are in"`. The skill was correct; the semantic router outranked it.

`SemanticIntentRouter` embeds each intent's `Description` + `ExamplePrompts` and
takes max cosine. Every `ChordInfoSkill` ExamplePrompt was **chord-symbol-shaped**:

```
What notes are in Dm7?          What notes are in a Cmaj7?
give me the notes of an F#m7b5  anatomy of a D7sus4 chord
```

**None** covered "what notes are in a *spelled-out quality* triad/chord".
Meanwhile `ScaleInfoSkill` carried **three** anchors of exactly that shape:

```
What notes are in C major?      Show me the notes in C major
List the notes in Bb major
```

So chord-quality queries sat closer to the scale skill. `bge-large` treats
`"what notes are in <root> <quality>"` as one cluster; the trailing noun
(triad / chord / scale) carries little weight against the shared prefix.

## Failed attempts — these are the useful part

### Attempt 1: chord anchors only → broke the scale side

Adding chord-quality anchors to `ChordInfoSkill` alone pulled *scale* queries
across. Measured: `"what notes are in the A minor scale"` — the word "scale"
right there in the query — went `scaleinfo 0.890` → `chordinfo 0.900`.

**Lesson:** anchors compete globally. Strengthening one intent weakens its
neighbours on shared phrasing. Fixes here must be two-sided.

### Attempt 2: a "9/9" score that was memorisation

An offline harness scored the change 9/9. Three of those test queries **were the
anchors just added** — and an anchor scores cosine 1.000 against itself.

Re-running on **held-out** phrasings gave the honest number: 8/12 → 10/12, and
that run is what exposed the Attempt-1 regression above.

**Lesson:** never evaluate an anchor change on queries present in the anchor set.

### Attempt 3: the offline simulation was not a valid proxy at all

After tuning to 19/20 offline, an A/B against a **separately rebuilt baseline
binary** showed:

```
"notes in c major"   baseline: skill.scaleinfo 0.93
                     patched : fallback-direct  0.00
```

A regression on a green corpus prompt that the simulation had *specifically*
predicted was safe (it scored scaleinfo 0.934 vs chordinfo 0.886).

Why the sim lied: it modelled 3 intents; production ranks many more. And losing
the race **cascades** — `chordinfo` wins the embedding, then
`ChordInfoSkill.CanHandle`/parse cannot read "c major" as a chord symbol, so the
query falls through to the LLM entirely.

**Lesson:** a few-intent cosine simulation generates *candidates*. It cannot
verify. Only a rebuilt-binary A/B can.

## Solution

Two-sided anchors that separate on the **trailing noun**, never on a bare
`<root> <quality>` (which is genuinely ambiguous and belongs to ScaleInfo).

`Common/GA.Business.ML/Agents/Skills/ChordInfoSkill.cs`:

```csharp
"what notes are in a C major triad",
"what are the notes of an A minor triad",
"what notes make up a G7 chord",
"which notes form a B diminished triad",
"spell a G7 chord",
"what notes are in an E major chord",
```

`Common/GA.Business.ML/Agents/Skills/ScaleInfoSkill.cs` — defensive, plus the
terse bare-key forms the chord anchors pull on:

```csharp
"what notes are in the A minor scale",
"what notes are in the G major scale",
"what notes are in D major",
"what notes are in the B flat major scale",
"notes in c major",   // <- the Attempt-3 regression
"notes in a minor",
"notes in g major",
```

Do **not** add a bare `<root> <quality>` beyond `D major` to ScaleInfo: a sweep
showed `"what notes are in F minor"` drags `"F sharp minor triad"` back across
(19/20 → 18/20).

## Verification (live, A/B against separately rebuilt binaries)

| prompt | before | after |
|---|---|---|
| what notes are in a G7 chord | `improvisation` 0.85 | `chordinfo` 0.98 → G B D F |
| what notes are in a C major triad | `scaleinfo` 0.90 | `chordinfo` 1.00 → C E G |
| which notes form a B minor triad | `scaleinfo` | `chordinfo` 0.94 → B D F# |
| notes in c major | `scaleinfo` 0.93 | `scaleinfo` 1.00 (held) |

Full prompt corpus: **0 failures**, 4m13s.

## Prevention

**Pin `routes_to`, not just content.** The corpus now asserts the route in both
directions. This is load-bearing: during the Attempt-3 regression the substring
invariants **passed throughout**, because the LLM fallback still produced a
correct-looking answer. A content-only corpus is structurally blind to routing
regressions.

**Both quality harnesses were blind to this bug, in the same way.**

- The prompt corpus had no "what notes are in X" entry at all.
- `routing-eval` (174 prompts) passed the *fix* at **Δ 0.0%** — its 10 chordinfo
  prompts are all chord-symbol-shaped, so the spelled-out register was never
  sampled. Tracked as [#560](https://github.com/GuitarAlchemist/ga/issues/560).

The general lesson: an eval set green for months does not mean the behaviour is
correct — it means the bug lives in a **register the eval does not sample**.
When adding a skill, ask which *phrasings* of its question are anchored, not
just whether the skill is covered.

## Related

- `docs/solutions/architecture/2026-05-30-chatbot-routing-is-embedding-first.md`
  — same subsystem, **different root cause** (a NullReferenceException in
  `CpuVoicingSearchStrategy.MatchesFilters`, not anchor shape). Both notes share
  a moral: verify against the live path before blaming a layer.
- Issue [#554](https://github.com/GuitarAlchemist/ga/issues/554) — spelled-out
  accidentals (`E-flat` → `E`) parse systemically wrong; surfaced by the same
  LLM-judge probe that found this.
- Memory: `feedback_offline_sim_not_valid_router_proxy`,
  `feedback_no_regex_routing_cheating`.

## Note on schema

Uses this repo's `docs/solutions/SCHEMA.md` convention
(`module / tags / problem_type` + decision-record fields), **not** the
compound-docs plugin schema — that schema's `component` enum is Rails/CORA
specific (`rails_model`, `brief_system`) and has no valid value for a .NET
semantic router.
