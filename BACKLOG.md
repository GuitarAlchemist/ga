# Guitar Alchemist Backlog

Ideas waiting to become features. One bullet per idea. When ready to build, run `/feature <idea>` — this launches brainstorm → plan → PR.

See `docs/plans/` for active plans.

---

## Guitarist Problems to Solve

These are real problems guitarists hit. They're the North Star for every feature.

### Ear Training & Recognition
- **"What key am I in?"** — a guitarist plays a progression by ear and wants GA to identify the key, suggest the scale, and show the diatonic chord set
- **"Why does this sound outside?"** — given a chord or note over a backing, explain which scale degrees are "outside" and why (tension vs. resolution)
- **"Is this a common substitution?"** — given two chords, tell the guitarist if there's a known substitution relationship (tritone sub, backdoor dominant, parallel minor, etc.)

### Chord & Voicing Discovery
- **"What can I play instead of this chord?"** — given a chord in context (key + position in progression), suggest functionally equivalent substitutions with different colour
- **"My hand hurts playing barre chords"** — suggest open-position or partial-barre voicings for any chord, ranked by fret-hand stretch
- **"I play in DADGAD / open D / open G"** — generate correct chord shapes for any alternate tuning, with voicing diagrams

### Improvisation & Scale Choice
- **"Which arpeggio fits this chord progression?"** — given Am F C G, suggest the arpeggios and scales that work over each chord with mode names
- **"How do I solo over a ii-V-I?"** — step-by-step: target notes, guide tones, chromatic approaches, bebop scale options
- **"Show me a lick in the style of [blues / jazz / country]"** — generate a 2-bar lick in ASCII tab + VexTab matching the stylistic vocabulary

### Practice & Learning
- **"I want to practice this scale in all positions"** — generate all 5 CAGED positions for any scale, with tab + fretboard diagram
- **"I keep forgetting chord tones"** — a drill: show a chord symbol → user names the intervals → GA verifies with GaChordIntervals
- **"How long until I can play this song?"** — technique gap analysis: compare required techniques to user's known skills, suggest a practice sequence

### Songwriting & Composition
- **"Help me finish this progression"** — given 2-3 chords, suggest 2-3 natural completions that cadence correctly, in the same key
- **"Make this progression more interesting"** — apply passing chords, secondary dominants, or borrowed chords to a plain I-IV-V
- **"What would this sound like in a minor key?"** — parallel minor / relative minor translation of an existing progression

### Technical / Gear
- **"How do I tune to drop-C?"** — fret-by-fret retuning guide with string tensions and chord shape adjustments
- **"Why does my tab look wrong?"** — parse ASCII tab and flag common notation errors (string order, timing symbols, missing barlines)

---

## Infrastructure Ideas

- **Live fretboard overlay** — show scale degrees on the React 3D fretboard in real time as the chatbot explains a concept
- **Chatbot chord diagram rendering** — when TheoryAgent mentions a chord, auto-generate a VexTab diagram inline in the chat response
- **BSP room chord assignment** — assign a diatonic chord function (I, ii, V…) to each BSP room and visualise harmonic flow through rooms
- **OPTIC-K similarity search UI** — let guitarists search by playing a rhythm pattern, find songs with similar harmonic structure

---

## How to Start a Feature

```bash
/feature <idea from above>
```

The `/feature` skill will:
1. Brainstorm with GA MCP tools to verify the music theory
2. Produce a plan in `docs/plans/`
3. Guide implementation grounded in the GA domain model
