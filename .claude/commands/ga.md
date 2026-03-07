Run a GA Language operation against the Guitar Alchemist domain services.

Arguments: $ARGUMENTS

## What to do

### Step 1 — Resolve the command

Parse the arguments. If they look like a natural language question rather than a
structured command, map them to the closest CLI command first:

| Natural language | Command |
|-----------------|---------|
| "chords in G major" / "G major scale" / "key of G" | `diatonic G major` |
| "chords in Bb minor" / "Bb minor key" | `diatonic Bb minor` |
| "what notes are in Am7" / "Am7 intervals" / "Am7 structure" | `intervals Am7` |
| "relative of A minor" / "relative key for C" | `relative A minor` / `relative C major` |
| "Am7 up a fifth" / "transpose Am7 by 7" | `transpose Am7 7` |
| "transpose Am F C G up 5" | `progression Am F C G --by 5` |
| "parse Cmaj9" / "what is Cmaj9" | `chord Cmaj9` |
| "list closures" / "available closures" | `closures` |
| anything resembling a question | `ask <question>` |
| "evolve" / "compound" / "improve the language" / "find patterns" | `evolve` |

### Step 2 — Run the command

Use the pre-built binary (0.1s) when it exists, fall back to `dotnet run` (3s):

```bash
# Fast path — use if binary exists
Apps/GaCli/bin/Debug/net10.0/ga.exe <args>

# Slow fallback — use if binary is missing
dotnet run --project Apps/GaCli/GaCli.fsproj -- <args>
```

### Command routing

| Arguments | CLI invocation |
|-----------|---------------|
| `chord <symbol>` | `ga chord <symbol>` |
| `intervals <symbol>` | `ga intervals <symbol>` |
| `transpose <symbol> <n>` | `ga transpose <symbol> <n>` |
| `diatonic <root> [major\|minor]` | `ga diatonic <root> [major\|minor]` |
| `relative <root> [major\|minor]` | `ga relative <root> [major\|minor]` |
| `progression <chords…> --by <n>` | `ga progression <chords…> --by <n>` |
| `closures [category]` | `ga closures [category]` |
| `ask <question>` | `ga ask <question>` |
| `evolve` | Run the compound engineering flywheel (see below) |
| *(no args or "help")* | `ga help` |

### Step 3 — Present the result

#### `chord` — parse a chord symbol

Raw: `{"root":"A","quality":"minor","components":["ext:7"],"bass":null}`

Present as:
> **Am7** = A minor triad with a minor 7th (m7)

Describe each component: quality gives the triad type, each extension adds an upper
interval. Name any bass note as a slash chord.

#### `intervals` — show interval content

Raw: `Am7:  P1  m3  P5  m7`

Present as:
> **Am7** contains: P1 (root) · m3 (minor third) · P5 (perfect fifth) · m7 (minor seventh)
>
> This is the minor 7th chord — a minor triad with a stacked minor third on top.

Add a one-sentence musical note about what makes this chord distinctive or how it's used.

#### `transpose`

Present as:
> **Cmaj9** → **F#maj9** (transposed up 6 semitones — a tritone)

Name the interval when possible: 1=m2, 2=M2, 3=m3, 4=M3, 5=P4, 6=TT, 7=P5, etc.

#### `diatonic`

Raw: array of 7 chord symbols

Present as a Roman numeral table:

> **Key of G major**
> ```
> I    ii   iii  IV   V    vi   vii°
> G    Am   Bm   C    D    Em   F#dim
> ```
> Common progressions in this key: I–V–vi–IV (G–D–Em–C), ii–V–I (Am–D–G)

#### `relative`

Raw: `A minor → relative: C major`

Present as:
> **A minor** and **C major** share the same key signature (no sharps or flats).
> Every chord in A minor's diatonic set is also found in C major.

Suggest running `ga diatonic` on both keys to see the shared chords.

#### `progression`

Raw: `Am F C G  →  Em C G D`

Present as:
> **Am F C G** transposed up 7 semitones (a perfect fifth) → **Em C G D**

#### `closures`

Present grouped by category as a compact table:

```
[Domain]
  domain.parseChord        Parse a chord symbol into its structure
  domain.chordIntervals    Return interval names for a chord
  domain.transposeChord    Transpose a chord by N semitones
  domain.diatonicChords    Get the 7 diatonic triads for a key
  domain.relativeKey       Get the relative major/minor key
```

#### `ask` — chatbot response

Show the agent's response verbatim, then offer 2–3 follow-up options relevant to the answer.

### Step 4 — Offer follow-ups

After every command, offer 1–3 relevant next steps. Examples:

- After `chord Am7`: "Try `intervals Am7` to see the full interval stack, or `transpose Am7 5` to move it up a fourth."
- After `diatonic G major`: "Try `relative G major` to find the relative minor, or `progression G D Em C --by 2` to transpose a common progression."
- After `intervals Cmaj9`: "Try `chord Cmaj9` to see the parsed structure, or `diatonic C major` to see all chords in this key."

#### `evolve` — compound engineering flywheel

`/ga evolve` kicks off the full compound engineering loop against recent work:

1. **Scope** — run `git log --oneline -10` and `git diff --stat HEAD~5..HEAD` to identify changed files
2. **Mine** — delegate to the `compound-researcher` agent with the changed file list
3. **Design** — delegate to the `fsharp-architect` agent with the researcher's top patterns
4. **Audit** — delegate to the `grammar-governor` agent to check for bloat/instability
5. **Report** — consolidate into a Compound Report and save to `docs/compound/<YYYY-MM-DD>-<branch>.md`

The full skill is documented in `.agent/skills/compound/SKILL.md`. Run `/compound` directly for the same effect, or use `/ga evolve` as a shorthand.

**Escalation rules**:
- Grammar governor returns `BLOCK PROMOTION` → stop; list what must be resolved first
- fsharp-architect proposes a Tier 3 DSL clause → require human sign-off before implementing
- 5+ occurrences of the same pattern → treat as P0, promote immediately

### If the server isn't running (ask command)

`ga ask` requires the GA API. If it fails with a connection error, say:
> The GA chatbot API isn't reachable. Start it with `pwsh Scripts/start-all.ps1` then retry.
> For music theory questions without the server, try `diatonic`, `intervals`, or `chord` instead.

## Examples

```
/ga chord Am7
/ga intervals Cmaj9
/ga transpose Cmaj9 5
/ga diatonic Bb minor
/ga relative A minor
/ga progression Am F C G --by 7
/ga closures domain
/ga ask what is a tritone substitution?
/ga chords in G major
/ga what notes are in Am7
/ga evolve
```
