---
name: "practice-routine"
description: "Suggests a structured practice routine for a stated goal — jazz comping, soloing, ear training, fingerstyle technique, etc. Pure catalog skill drawing on guitar-pedagogy templates. Use when a learner asks 'give me a practice plan for X' / 'how do I practice Y'."
triggers:
  - "practice routine"
  - "practice plan"
  - "practice schedule"
  - "how do i practice"
  - "what should i practice"
  - "drill"
  - "exercises for"
  - "improve at"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "deterministic-catalog"
  origin: "drafted in skills-dev/ as Tier 4 catalog skill (skill-stewards 2026-05-05)"
  evidence-kinds:
    - catalog_lookup
---

# Practice Routine Suggestions

Catalog skill — when a user asks for a practice plan, match their goal to one of the templates below and reproduce verbatim with light personalisation (their stated key, tempo, or instrument).

## How to dispatch

User states a goal (jazz comping, fingerstyle, soloing, ear training, scales, technique, repertoire). Match to the closest template; if none fit, ask one clarifying question before defaulting.

## Templates

### 🎹 Jazz comping (rhythm guitar / piano)

Daily 30-min routine:
1. **Warm-up (5 min)** — drop-2 voicings of one ii-V-I in any key, both hands.
2. **Voicing inventory (10 min)** — pick a tune (e.g. *Autumn Leaves*); play 3 different voicings of each chord.
3. **Comping rhythms (10 min)** — Charleston, *bossa*, four-on-the-floor; each over the same ii-V-I.
4. **Free comping (5 min)** — over a backing track, no charts.

Skills you can chain: `voicing-search` (find voicings), `voice-leading` (smooth between them).

### 🎸 Soloing / improvisation

Daily 30-min routine:
1. **Scale warm-up (5 min)** — one major scale + its modes in one position.
2. **Arpeggio drilling (10 min)** — arpeggios of the chords in your target tune, ascending and descending.
3. **Target-tone practice (10 min)** — over a backing track, land on chord tones at the bar line.
4. **Free solo (5 min)** — record yourself; listen back.

Skills you can chain: `arpeggio` (find arpeggios over chords), `progression-analysis` (understand your tune's harmony).

### 👂 Ear training

Daily 15-min routine:
1. **Interval recognition (5 min)** — major and minor 3rds, perfect 4ths and 5ths, octaves, then sevenths.
2. **Chord-quality recognition (5 min)** — major / minor / diminished / augmented / dominant 7th / major 7th.
3. **Diatonic-degree recognition (5 min)** — in a fixed key, which scale degree is the melody on?

Tools: any ear-training app (functional-ear-trainer, EarBeater, Tenuto). The chatbot can quiz you on intervals if you ask.

### 🎶 Fingerstyle technique

Daily 20-min routine:
1. **Right-hand independence (5 min)** — alternate-bass over a held chord (e.g. C: bass C–G–C–E with melody on top).
2. **Travis picking (5 min)** — thumb on bass, fingers on top three strings, classic 3+3+2 pattern.
3. **Tune work (10 min)** — one section of an arrangement (e.g. Tommy Emmanuel, Chet Atkins, Don Ross).

### 📈 Repertoire building

Weekly: **1 tune per week** drilled to memory. Pick one across genres each week (jazz standard, bluegrass tune, pop song, classical piece). After 12 weeks, you have 12 tunes you can pull out anywhere.

### 🧘 Technique: barre chords / left hand

Daily 10-min routine:
1. **Single-finger barre (3 min)** — index finger across all 6 strings, find the cleanest position (usually 5th–7th fret), play one note at a time.
2. **F major barre (3 min)** — full E-shape barre, 30 seconds at a time, rest, repeat.
3. **B major barre (4 min)** — A-shape barre, switch between B and C every measure.

## How long to commit

Realistic ranges:
- **15 min/day** — incremental progress on one focus area.
- **30–60 min/day** — meaningful improvement on 2 focus areas.
- **2+ hr/day** — pre-professional / serious-amateur intensity.

Pick the time you can SUSTAIN, not the time you'd ideally like.

## When to refuse / clarify

- *"Make me a 6-month curriculum"* — too much; offer the 30-day starter and reassess.
- Goals outside guitar / piano contexts (e.g. *"how do I practice singing"*) — defer.

## When to call other skills

- *"What chords should I practice for jazz?"* → `diatonic-chords` for the key, `chord-info` for each.
- *"Voicings for comping"* → `voicing-search`.
- *"Arpeggios for my solo"* → `arpeggio`.
