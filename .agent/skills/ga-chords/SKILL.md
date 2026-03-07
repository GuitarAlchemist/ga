---
name: "GA Chord Theory Helper"
description: "Perform live chord operations — parsing, transposition, diatonic analysis — backed by the real GA domain services. Use when working with chord symbols, progressions, or key relationships in code."
---

# GA Chord Theory Helper

Use this skill when a task involves **chord symbols, progressions, or key relationships** and you need ground-truth answers from the live GA domain services rather than reasoning from memory.

## When to Use

- Writing test data and need the correct diatonic chords for a key
- Transposing a chord progression in a test file or example
- Verifying a chord symbol is well-formed before adding it to domain data
- Understanding what intervals make up a complex chord like `C7#9b13`

## Step-by-Step Procedure

### 1. Parse a chord symbol

```bash
curl -sk -X POST https://localhost:7001/api/ga/eval \
  -H "Content-Type: application/json" \
  -d "$(jq -n --arg s 'invoke "domain.parseChord" (Map.ofList ["symbol", box "C7#9"])' '{script:$s}')"
```

**Returns** JSON like:
```json
{"root":"C","quality":"dominant","components":["ext:7","alt:#9"],"bass":null}
```

Present as: **C7#9** = root C, dominant 7th, sharp-9 extension.

### 2. Transpose a chord or progression

For a single chord:
```fsharp
invoke "domain.transposeChord" (Map.ofList ["symbol", box "Am7"; "semitones", box 5])
// → "Dm7"
```

For a full progression (e.g. `Am F C G` up 5 semitones), build a loop:
```fsharp
let chords = [| "Am"; "F"; "C"; "G" |]
chords |> Array.map (fun s ->
    match invoke "domain.transposeChord" (Map.ofList ["symbol", box s; "semitones", box 5]) with
    | Ok v  -> v :?> string
    | Error e -> sprintf "ERROR:%A" e)
```

Or run it as a single script via the eval endpoint.

### 3. Get diatonic chords for a key

```fsharp
invoke "domain.diatonicChords" (Map.ofList ["root", box "Bb"; "scale", box "major"])
// → [| "Bb"; "Cm"; "Dm"; "Eb"; "F"; "Gm"; "Adim" |]
```

Scale options: `"major"` · `"minor"` · `"aeolian"` · `"natural minor"`

Display the result as Roman numerals:
```
Key of Bb major:  I=Bb  ii=Cm  iii=Dm  IV=Eb  V=F  vi=Gm  vii°=Adim
```

### 4. Transpose an entire file's chord symbols

When the user has a file containing chord progressions (test files, YAML configs, markdown docs):

1. Read the file with the `Read` tool
2. Extract all chord symbols using a regex: `[A-G][#b]?(?:maj|min|m|dim|aug|sus)?[0-9]*(?:/[A-G][#b]?)?`
3. Build a batch transposition script:
   ```fsharp
   let transpose sym n =
       invoke "domain.transposeChord" (Map.ofList ["symbol", box sym; "semitones", box n])
   [| "Am7"; "Dm7"; "G7"; "Cmaj7" |] |> Array.map (fun s -> transpose s 2)
   ```
4. Apply the results back to the file with Edit

### 5. Batch analysis pipeline

To parse and analyze several chords at once:
```fsharp
ga {
    let chords = [| "Cmaj7"; "Am7"; "Dm7"; "G7" |]
    let! results =
        chords
        |> Array.map (fun s ->
            GaClosureRegistry.Global.Invoke("domain.parseChord", Map.ofList ["symbol", box s]))
        |> Array.toList
        |> fanOutAll
    return results
} |> run
```

## Semitone Reference

| Interval | Semitones | Example |
|----------|-----------|---------|
| Minor 2nd | 1 | C → C# |
| Major 2nd | 2 | C → D |
| Minor 3rd | 3 | C → Eb |
| Major 3rd | 4 | C → E |
| Perfect 4th | 5 | C → F |
| Tritone | 6 | C → F# |
| Perfect 5th | 7 | C → G |
| Minor 6th | 8 | C → Ab |
| Major 6th | 9 | C → A |
| Minor 7th | 10 | C → Bb |
| Major 7th | 11 | C → B |
| Octave | 12 | C → C |

Negative values transpose down: `-5` = down a perfect 4th.

## Validating New Domain Data

Before adding a chord to `GA.Business.Config` YAML or a unit test:

1. Parse it with `domain.parseChord` — if it errors, the symbol is malformed
2. Check the returned quality/components match intent
3. For scale configs, call `domain.diatonicChords` and verify the 7 degrees match expected chord qualities

This is faster than running the full test suite for a quick sanity check.
