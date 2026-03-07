---
name: "GA Language Evaluator"
description: "Run GA Language (GAL) scripts against the live GaApi FSI session. Use when you need to interactively explore domain closures, test pipelines, or compute music-theory results against real GA data."
---

# GA Language Evaluator

Use this skill when the user asks to **run a GAL script**, **test a closure**, or **explore the GA DSL** interactively.

## When to Use

- "Run this GA script"
- "What does `domain.diatonicChords` return for E minor?"
- "Can you test this pipeline step?"
- "List all available closures"

## Endpoint

```
POST https://localhost:7001/api/ga/eval
Content-Type: application/json
{"script": "<fsharp expression>"}

GET  https://localhost:7001/api/ga/closures[?category=domain|pipeline|agent|io]
```

Override host with `GA_API_BASE_URL` env var if the server runs on a different port.

## Script Syntax

Scripts run inside a pre-loaded FSI session (`GaPrelude.fsx`). Everything in `GA.Business.DSL` is in scope.

### Simple closure call (use `invoke` shorthand)
```fsharp
invoke "domain.parseChord" (Map.ofList ["symbol", box "Am7"])
```

### Multi-step pipeline (use `ga { }` computation expression)
```fsharp
ga {
    let! parsed   = GaClosureRegistry.Global.Invoke("domain.parseChord",
                        Map.ofList ["symbol", box "Cmaj9"])
    let! diatonic = GaClosureRegistry.Global.Invoke("domain.diatonicChords",
                        Map.ofList ["root", box "C"; "scale", box "major"])
    return diatonic
} |> run
```

### List all closures
```fsharp
listClosures ()
```

### Parallel fan-out
```fsharp
invoke "agent.fanOut" (Map.ofList [
    "question",   box "What is a Neapolitan chord?"
    "agentNames", box [| "agent.theoryAgent" |]
])
```

## Running a Script via Bash

```bash
# Simple invocation (use -sk for localhost HTTPS with self-signed cert)
curl -sk -X POST https://localhost:7001/api/ga/eval \
  -H "Content-Type: application/json" \
  -d '{"script": "invoke \"domain.diatonicChords\" (Map.ofList [\"root\", box \"G\"; \"scale\", box \"major\"])"}' \
  | python -m json.tool
```

For multi-line scripts, write the script to a temp file and use `jq` to build the payload:

```bash
SCRIPT='ga {
    let! chords = GaClosureRegistry.Global.Invoke("domain.diatonicChords",
                      Map.ofList ["root", box "F"; "scale", box "major"])
    return chords
} |> run'

curl -sk -X POST https://localhost:7001/api/ga/eval \
  -H "Content-Type: application/json" \
  -d "$(jq -n --arg s "$SCRIPT" '{script: $s}')"
```

## Interpreting Results

The endpoint returns:
```json
{
  "success": true,
  "output":  "(any stdout from the FSI session)",
  "error":   null,
  "value":   "(string representation of the return value)"
}
```

- **`value`**: the result of the expression — may be a JSON string (from `domain.*` closures), an F# array repr, or a plain string from agent closures.
- **`output`**: any `printfn` / `listClosures()` output.
- **`error`**: compilation or runtime error message.

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

Full list at runtime: `GET /api/ga/closures`

## Formatting Output

- For chord arrays (`string[]`), display as a numbered list or a one-liner like `I=C  ii=Dm  iii=Em  IV=F  V=G  vi=Am  vii°=Bdim`
- For JSON chord structures, parse and show root + quality + extensions in plain language
- For agent responses, display the text verbatim then offer follow-up options
