# chatbot-eval

End-to-end evaluation of the GA chatbot: conversations against a running GaApi, an LLM judge over the
answers and the agentic traces, and a gate that compares a run with a baseline. CI runs it in
`.github/workflows/chatbot-eval.yml`.

| Command | What it does |
|---|---|
| `convo <base_url> <cases.yaml> <out.json>` | Plays each case (one or more turns, with history) against `POST /api/chatbot/chat` and applies the deterministic checks (`any`, `all`, `min_len`, error markers). Exit 1 when a case fails, 2 on bad input. |
| `judge -backend api -model claude-haiku-5-5 -out verdicts.jsonl <convo.json>...` | Grades every case (correctness, helpfulness, context, routing, trace issues). Reads `GA_ANTHROPIC_API_KEY`, and `GA_ANTHROPIC_WORKSPACE_ID` for a key not scoped to a workspace. About $0.03 for the 51 cases. |
| `evalgate -base-convo a.json,b.json -base-judge base.jsonl -convo ... -judge ... -tolerance N -summary out.md` | A case is good when its checks pass and the judge does not fail it. Lists regressions, improvements and cases still failing; exit 1 when regressions exceed the tolerance. |
| `traceana <convo.json>...` | Latency and step breakdown from the agentic traces. |
| `judgecmp <a.jsonl> <b.jsonl>` | Verdict agreement between two judge runs. |

Cases live in `cases/`. Build everything with `go build -o bin/ ./cmd/...`.
