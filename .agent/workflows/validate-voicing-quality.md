---
description: How to validate the quality of voicing search results
---

To validate the quality of the voicing search engine and the integrity of the index, you can use the `benchmark-quality` command in the GaCLI tool.

## Overview

The `benchmark-quality` command executes a suite of predefined queries (benchmarks) against the local MongoDB index. It evaluates the results based on specific heuristics, such as:
- **Difficulty consistency**: Ensuring "Beginner" chords don't have high difficulty scores or barre requirements.
- **Semantic alignment**: Ensuring queries for "sad" chords return minor/diminished playing or appropriate semantic tags.
- **Structural integrity**: Ensuring "Shell" voicings adhere to expected note counts and characteristics.

## Usage

Run the following command in the terminal:

```powershell
dotnet run --project GaCLI/GaCLI.csproj -- benchmark-quality
```

## Adding New Benchmarks

To add new quality checks:
1. Open `GaCLI/Commands/BenchmarkQualityCommand.cs`.
2. Add a new `BenchmarkCase` to the `benchmarks` list.
3. Define the `Name`, `ValidatedOptions` (the query), and the `Validator` function.

The validator function receives the list of `VoicingEntity` results and returns a tuple:
- `Score` (double, 0.0 - 1.0): 1.0 is perfect.
- `Issues` (List<string>): A list of text descriptions of what failed.

## Example Output

```text
╭───────────────────────┬───────┬────────╮
│ Benchmark             │ Score │ Issues │
├───────────────────────┼───────┼────────┤
│ Beginner Open C       │ 100%  │ None   │
│ Sad Funk Context      │ 100%  │ None   │
│ Jazz Shells           │ 100%  │ None   │
│ Upper Structure Logic │ 100%  │ None   │
╰───────────────────────┴───────┴────────╯

Overall Quality Score: 100%
```
