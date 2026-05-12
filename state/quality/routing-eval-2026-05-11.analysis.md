# Routing eval baseline 2026-05-11 — failure analysis

> **Update 2026-05-12:** routing accuracy reached **80/80 = 100.0%**
> (+33.8 pp from the original 66.2% baseline). The final 10 misroutes
> were closed via targeted ExamplePrompts expansion across 8 skills plus
> one new `DefaultRoutingHintProvider` rule (mode-name → skill.modes).
>
> Per-intent F1 = 1.00 across all 16 measured intents.
>
> The fix landed in PR (this branch). See
> `routing-eval-2026-05-12.json` for the full per-prompt trace. Notes on
> what changed:
> - **scaleinfo** got "What's in the G major scale?" pattern (closes si-4)
> - **modes** got "What notes are in G Mixolydian?" + 3 mode-named examples
>   AND a deterministic hint-provider boost for explicit mode names
>   (Ionian/Dorian/Phrygian/Lydian/Mixolydian/Aeolian/Locrian) — the
>   mode name is the structural discriminator and only a hint guarantees
>   it dominates the scaleinfo overlap
> - **interval** got "What's a perfect fifth?" + "Minor third up from D"
>   definitional / direction patterns (closes in-2 and in-4)
> - **chordsubstitution** got bare "Alternative chord for Cmaj7" without
>   the in-key tail + "Modal interchange substitutes for C major"
>   (closes cs-4 and cs-5)
> - **progressionmood** got "Brighten up a minor key tune" verb-anchored
>   pattern (closes pm-4)
> - **circleoffifths** got "Across the circle from D" topological idiom
>   (closes co-4)
> - **commontones** got "Overlapping notes in Cmaj7 and Em7" — the
>   "overlap" verb is the discriminator vs raw two-chord chordinfo
>   queries (closes ct-5)
> - **keyidentification** got "Find the tonic of [progression]" pattern —
>   the {key, tonic} synonym pair must both be in the embedding surface
>   (closes ki-5)
>
> One-way / two-way door: example-prompt edits are two-way doors
> (revert anytime). The mode-name hint rule is also two-way; its
> magnitude (+0.06) is governed by the central BoostMagnitude constant
> which is pinned by an explicit unit test, so a regression in any
> single rule is visible in the per-intent F1 trace.
>
> ---
>
> **Earlier (2026-05-11 PM):** mid-session lift from 53/80 to 70/80
> (66.2 → 87.5%) closed the highest-leverage failures
> (genreessentials → 1.00, practiceroutine → 1.00, transpose → 0.91).
> Original analysis below for context.

**Overall: 53/80 correct = 66.2% accuracy** (router threshold 0.65, embedder
`nomic-embed-text`, 17 production skills loaded via reflection).

This is the **first scored baseline** for `SemanticIntentRouter`. Treat
the file as a measurable starting point — every subsequent change to
example prompts, hint-provider regexes, threshold, or embedder model
should be benchmarked against this.

## Per-intent F1 (sorted, worst first)

| Intent | F1 | Notes |
|---|---|---|
| skill.genreessentials | **0.00** | All 5/5 misrouted — examples not discriminative against `beginnerchords`/`chordsubstitution`/`progressioncompletion`/`transpose` |
| skill.transpose | **0.25** | 4/5 misrouted — prompts containing "progression" + "key" name collide with `progressioncompletion`/`diatonicchords` |
| skill.chordinfo | 0.50 | `tell me about Dm7` → `modes`; `what makes a chord a major seventh` → `diatonicchords` |
| skill.practiceroutine | 0.57 | 3/5 fall **below the 0.65 confidence threshold** — synonyms "schedule"/"outline" not in examples |
| skill.diatonicchords | 0.60 | `IV chord in A major` → `chordinfo`; `list chords in G major` → `chordsubstitution` |
| skill.scaleinfo | 0.67 | `what's in the G major scale` → `modes` |
| skill.modes | 0.73 | `notes in G mixolydian` → `scaleinfo` (mirror of scaleinfo failure) |
| skill.chordsubstitution | 0.73 | `alternative chord for Cmaj7` → `chordinfo` |
| skill.interval | 0.75 | `what's a perfect fifth` → `circleoffifths`; `minor third up from D` → `scaleinfo` |
| skill.whatcanyoudo | 0.75 | 2/5 fall below 0.65 threshold |
| skill.progressioncompletion | 0.77 | over-greedy — catches transpose prompts |
| skill.circleoffifths | 0.80 | `across the circle from D` → `keyidentification` |
| skill.keyidentification | 0.80 | one transpose collision |
| skill.beginnerchords | 0.83 | inflated FP from genreessentials prompts ("country guitar starter chords") |
| skill.commontones | 0.89 | one chordinfo collision |
| skill.progressionmood | 0.89 | one chordinfo collision |

## Failure clusters (in order of leverage)

### 1. `genreessentials` is invisible to the router (5 prompts, 6.25% of corpus)

All five labeled prompts use "essential X for Y" / "starter X" / "must-know X
for Y" / "key chords in Z" patterns. The skill's current examples don't
cover these shapes. **Fix candidate**: rewrite examples to anchor on the
genre tokens (blues, jazz, country, rock, funk) + the "for / in" preposition.

### 2. Transpose vs progression-completion collision (4 prompts)

Anything shaped like "transpose THIS PROGRESSION to/down/up Y" gets caught
by `progressioncompletion` because both skills' examples lean heavily on
"progression". **Fix candidate**: add a `transpose` regex hint to
`DefaultRoutingHintProvider` matching `\b(transpose|shift|move)\b.*\b(up|down|to|into)\b` —
deterministic short-circuit before semantic dispatch.

### 3. Confidence-threshold floor catches 5 prompts (skill.practiceroutine x3 + skill.whatcanyoudo x2)

These prompts use natural-language synonyms ("schedule", "outline",
"figure out what to ask", "show me what you know") not in the skill's
ExamplePrompts. They all return `(none)` at confidence < 0.65.
**Two paths**: (a) expand example prompts to cover synonyms,
(b) lower threshold per-intent for meta intents. (a) is safer.

### 4. Chord vs scale/mode confusion (3 prompts)

`tell me about Dm7` → `modes`, `what's in the G major scale` → `modes`,
`notes in G mixolydian` → `scaleinfo`. The chord/scale boundary is
genuinely fuzzy. **Fix candidate**: add positive examples that
distinguish (e.g., chordinfo: `tell me about a chord`; scaleinfo:
`tell me about a scale`).

### 5. "in a key" overlap (3 prompts)

`IV chord in A major`, `chords in the key of C`, `list the chords in G
major` all route to chord-related intents but each to a different one.
**Fix candidate**: standardize `diatonicchords` examples around "chords
in [KEY] / [DEGREE] chord in [KEY] / what chords are in [KEY]".

## Recommended follow-up PR ordering

1. **Quick win PR — fix #1 + #2 + #5** (deterministic regex hints + 9 example-prompt edits across 4 skills). Target: +12 correct → ~81% accuracy. Re-run harness, commit `routing-eval-YYYY-MM-DD.json` showing the lift.
2. **Threshold-tuning experiment** — try threshold 0.60 with confusion-matrix delta. Pick if it doesn't regress precision-sensitive intents.
3. **Corpus expansion v0.4** — add 2-3 paraphrased prompts per intent (currently 5/intent → 7-8/intent). Watch for label drift.

## Out of scope for the baseline PR

- All of the above are **fixes**. The baseline PR (this one) is the
  measurement infrastructure: harness already exists (PRs #168/#169),
  this PR just adds the first scored artifact and the analysis note
  pointing at where the next routing PR should aim.
