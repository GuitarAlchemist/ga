---
name: "GA Eval"
description: "Run GA Language (GAL) operations against the live domain services via the ga CLI. Use when you need to interactively explore domain closures, test pipelines, or compute music-theory results."
---

# /ga eval — GA Language Evaluator

Use this sub-command when the user asks to **run a GAL script**, **test a closure**, or **explore the GA DSL** interactively.

## When to Use

- "What does `domain.diatonicChords` return for E minor?"
- "List all available closures"
- "Run this closure and show me the result"
- "Test whether this chord symbol parses correctly"

## CLI (primary — no server needed for domain closures)

The `GaCli` project (`Apps/GaCli`) calls domain closures directly in-process. No Aspire stack required.

```bash
# From repo root — build once, then use the binary directly
dotnet build Apps/GaCli/GaCli.fsproj -c Debug -v q

# Run any command
dotnet run --project Apps/GaCli/GaCli.fsproj -- <command>
```

### Commands

```
ga closures [domain|pipeline|agent|io]   List closures (optionally filtered by category)
ga chord <symbol>                         Parse a chord symbol → JSON structure
ga transpose <symbol> <semitones>         Transpose a chord by N semitones
ga diatonic <root> [major|minor]          Get the 7 diatonic triads for a key
ga progression <sym...> --by <n>          Transpose every chord in a progression
ga ask <question>                         Ask the GA chatbot (requires server running)
```

### Examples

```bash
dotnet run --project Apps/GaCli/GaCli.fsproj -- closures
dotnet run --project Apps/GaCli/GaCli.fsproj -- chord Am7
dotnet run --project Apps/GaCli/GaCli.fsproj -- transpose Cmaj9 5
dotnet run --project Apps/GaCli/GaCli.fsproj -- diatonic G major
dotnet run --project Apps/GaCli/GaCli.fsproj -- progression Am F C G --by 7
```

## Available Closures (quick reference)

| Name | Inputs | Output |
|------|--------|--------|
| `domain.parseChord` | `symbol: string` | JSON chord structure |
| `domain.transposeChord` | `symbol: string`, `semitones: int` | transposed chord symbol |
| `domain.diatonicChords` | `root: string`, `scale: string` | string[] of 7 triads |
| `agent.theoryAgent` | `question: string` | GA chatbot answer |
| `agent.tabAgent` | `request: string` | tab or VexTab string |
| `agent.criticAgent` | `progression: string` | harmonic critique |
| `agent.fanOut` | `question: string`, `agentNames: string[]` | Map of answers |
| `io.readFile` | `path: string` | file contents |
| `io.writeFile` | `path: string`, `content: string` | unit |
| `io.httpGet` | `url: string` | response body |
| `io.httpPost` | `url: string`, `body: string` | response body |

Full list at runtime: `dotnet run --project Apps/GaCli/GaCli.fsproj -- closures`

## When the Aspire Stack Is Running

For agent closures (theory/tab/critic) and FSI scripts, the GaApi must be up.
The eval endpoint accepts raw F# expressions:

```bash
curl -s -X POST http://localhost:5232/api/ga/eval \
  -H "Content-Type: application/json" \
  -d "$(jq -n --arg s 'invoke "domain.diatonicChords" (Map.ofList ["root", box "G"; "scale", box "major"])' '{script:$s}')"
```

## Formatting Output

- Chord JSON `{"root":"A","quality":"minor","components":["ext:7"],"bass":null}` → **Am7** = A minor, minor 7th extension
- Diatonic arrays → display as Roman numeral table: `I=G  ii=Am  iii=Bm  IV=C  V=D  vi=Em  vii°=F#dim`
- Agent responses → display verbatim, then offer follow-up
