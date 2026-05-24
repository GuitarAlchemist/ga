# Semantic-regression artifacts

One JSON per PR head SHA, written by `.github/workflows/semantic-regression-chatbot.yml`
(producer script: `Scripts/replay-chatbot-goldens.ps1`).

## What this closes

Today `state/quality/chatbot-qa/golden-traces/<slug>/` stores per-prompt
reference answers + median elapsed + agent + verified date (surfaced as QA
badges in `/chatbot/#showcase`). `Scripts/compare-trace-to-canonical.ps1`
catches drift in the **trace shape** (steps, agent ids, attributes), but
nothing automated catches drift in the **answer text itself** — a router
or skill change can keep the trace shape pristine while replacing a 3 KB
explanation of the circle of fifths with a one-line shrug.

This harness closes that gap: replay each golden, embed both sides, diff
cosine, post a PR comment, persist the JSON for the dashboard.

## Schema (semantic-regression-v1)

```json
{
  "schema": "semantic-regression-v1",
  "head_sha": "abc123...",
  "run_at": "2026-05-23T18:42:00Z",
  "chatbot_url": "http://localhost:5252",
  "encoder": "text-embedding-3-small",
  "threshold_cosine": 0.85,
  "sample_size": 0,
  "elapsed_seconds": 42.7,
  "cost_warning": null,
  "prompts": [
    {
      "slug": "explain-the-circle-of-fifths",
      "prompt": "Explain the circle of fifths",
      "category": "theory",
      "provider_ref": "skill.circleoffifths",
      "provider_new": "skill.circleoffifths",
      "ref_cosine_self": 1.0,
      "new_cosine": 0.91,
      "delta": -0.09,
      "verdict": "ok",
      "tokens": 1840,
      "error": null
    }
  ],
  "summary": {
    "total": 14,
    "ok": 13,
    "drift": 1,
    "errored": 0,
    "provider_mismatch": 0,
    "no_baseline": 0,
    "total_tokens": 28910
  }
}
```

### Verdict values

| verdict | meaning |
|---|---|
| `ok` | `cosine ≥ threshold`. No semantic drift detected. |
| `drift` | `cosine < threshold` (default 0.85). Answer text moved enough to be worth a human review. Advisory only — does **not** fail the build. |
| `provider_mismatch` | The PR routed this prompt to a different agent (`provider_new ≠ provider_ref`). That's a routing test, not a semantic regression — cosine is omitted. |
| `errored` | Replay or embedding failed. Surfaced as a workflow failure if every prompt errors; ignored otherwise. |

## Encoder choice

`text-embedding-3-small` (1536 dims, OpenAI). Documented in the workflow YAML
header. Why not `ga_generate_voicing_embedding` (the GA MCP voicing
embedder)? That tool produces 228-dim vectors from raw pitch-class
geometry — perfect for voicings, useless for paragraphs of markdown
English. Chatbot answers are prose. `text-embedding-3-small` is already
the project's standing prose embedder (`Apps/ga-server/GaApi/Services/
VectorSearchService.cs`, `Tools/GaDataCLI/`).

One encoder per run. The `encoder` field is recorded so the dashboard
never compares cosines across encoders.

## Cost guardrail

Each prompt makes 2 embedding calls (reference + new answer). With ~45
goldens × ~4 KB avg answer ≈ ~90k input tokens per full run.
`text-embedding-3-small` is $0.02 / 1M input tokens → roughly **$0.0018
per PR run** at full corpus. If a single run exceeds 50k input tokens
the artifact's `cost_warning` field is populated and the PR comment
surfaces it. Use the workflow's `sample_size` input (or the script's
`-SampleSize N` parameter) to subsample if cost becomes a concern, or
move the workflow off per-PR onto a weekly cron.

## Retention

90 days as workflow artifacts (GitHub default). The on-disk
`<sha>.json` files in this directory are committed and live for the
life of the repo — they're small (~5–15 KB) and the dashboard's QA tab
needs the history to draw a trend line. Prune via the same rotation
that `state/quality/chatbot-qa/*.json` daily snapshots use if/when
volume becomes a problem (currently ~1 file per merged PR; ~365 / yr
upper bound assuming a merged PR every day).

## Running locally

```powershell
# Start the chatbot first (Apps/GaChatbot.Api on :5252).
# Then:
$env:OPENAI_API_KEY = "sk-..."
pwsh Scripts/replay-chatbot-goldens.ps1
```

Outputs go to `state/quality/chatbot-qa/semantic-regression/<HEAD>.json`
(local HEAD SHA by default; override with `-HeadSha`).

Optional: `-EmitMarkdown path/to/comment.md` writes the PR-comment table.
