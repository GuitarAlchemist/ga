Run a GA Language operation against the Guitar Alchemist domain services.

Arguments: $ARGUMENTS

## What to do

Parse the arguments and run the corresponding `ga` CLI command from the repo root using:

```
dotnet run --project Apps/GaCli/GaCli.fsproj -- <args>
```

### Argument routing

| Arguments | Command to run |
|-----------|---------------|
| `chord <symbol>` | `-- chord <symbol>` |
| `transpose <symbol> <n>` | `-- transpose <symbol> <n>` |
| `diatonic <root> [major\|minor]` | `-- diatonic <root> [major\|minor]` |
| `progression <chords…> --by <n>` | `-- progression <chords…> --by <n>` |
| `closures [category]` | `-- closures [category]` |
| `ask <question>` | `-- ask <question>` |
| *(no args)* | `-- help` |

### Output formatting

After running the command, present the result clearly:

- **chord**: parse the JSON and describe it in plain language.
  `{"root":"C","quality":"major","components":["ext:9"],"bass":null}` → **Cmaj9** = C major with added major 9th
- **transpose**: show `<original> → <transposed>` with the interval name
- **diatonic**: show as Roman numerals: `I=G  ii=Am  iii=Bm  IV=C  V=D  vi=Em  vii°=F#dim`
- **progression**: show the before → after on one line
- **closures**: show grouped by category as a compact table
- **ask**: show the agent's response verbatim, then offer follow-up options

### If the server isn't running (agent closures)

`agent.*` closures need the GA API running. If `ga ask` fails with a connection error, say:
> The GA chatbot API isn't reachable. Start it with `pwsh Scripts/start-all.ps1` then retry.

### Examples

```
/ga chord Am7
/ga transpose Cmaj9 5
/ga diatonic Bb minor
/ga progression Am F C G --by 7
/ga closures domain
/ga ask what is a tritone substitution?
```
