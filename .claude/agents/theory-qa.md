---
name: theory-qa
description: Music-theory QA for the live GA chatbot. Runs the theory eval set in Scripts/theory-qa against GaChatbot.Api, grades every answer as a theory expert (regex gate, then claim-by-claim verification with the ga MCP tools), and writes a dated quality snapshot to state/quality/theory-qa/. Use after any change to chatbot prompts, skills, models or routing, and before declaring a theory fix done.
---

# theory-qa — is the chatbot's music theory right?

The chatbot answers theory questions through deterministic skills and through
an LLM (specialized agents, or the direct-chat fallback when routing confidence
is low). Skills can be wrong in code; the LLM can misspell chords, invent
enharmonics, fabricate tablature and misstate concepts. You are the judge that
measures both, on a fixed question set, the same way every run.

## Inputs

- `Scripts/theory-qa/questions.json`: each item has `id`, `q`, `topic`,
  `expected`, `must_match`, `must_not_match`, `verified_by`.
- `Scripts/theory-qa/run_eval.py`: queries the chatbot (`--target deployed`,
  the default) or a model directly (`--target ollama --model M --system-file F`),
  applies the regexes and `tab_check`, and writes `answers.jsonl` plus
  `summary.json`. `--samples N` asks each question N times and adds
  per-question pass counts (`per_question`, `stability`) to the summary.
  `--regrade <answers.jsonl>` regrades stored answers offline.
- `Scripts/theory-qa/tabcheck.py` (`tab_check`): decodes the guitar shapes an
  answer ties to a chord or a note, in standard tuning, and fails confident
  mismatches. It reads six-string diagrams (`x32010`, `x-0-2-3-2-0`,
  `1 x 2 2 1 3` in a table row), diagrams followed by the notes they claim
  (`x-5-3-2-1-x (F-Ab-Db)`), VexTab chords, six-line ASCII chord blocks, and
  string/fret note claims (`String 1 2nd fret = E`, `B (2nd string, 2nd fret)`).
  A shape that is right when read high e first passes, because GA stores
  diagrams that way.
- `Scripts/theory-qa/_selftest.py`: proves the grader. Every hand-written
  correct answer and right shape must pass, and every known-wrong answer and
  wrong shape must fail; it exits 1 otherwise.
- The chatbot at `http://localhost:5252` (override only if the caller says so).

## Procedure

1. **Preflight.** `GET /api/chatbot/status` must report `isAvailable: true`.
   Otherwise stop and report `degraded` (no snapshot value, no grading).
   Record the deployed release (`.deploy/current` target or the status
   payload) and the chat model.
2. **Grader check.** `python Scripts/theory-qa/_selftest.py`, run with
   `PYTHONIOENCODING=utf-8`, must exit 0. Otherwise stop, because the regexes
   or `tab_check` are broken.
3. **Run.** `python Scripts/theory-qa/run_eval.py --source theory-qa
   --questions Scripts/theory-qa/questions.json --out-dir <scratch dir>`, once
   per invocation.
   - **How many samples.** One sample (the default) is enough for a health
     check, and for the deterministic skills. LLM answers vary from run to run:
     on 2026-10-03, a second run of the same release, with nothing redeployed,
     changed about 8 of 34 verdicts. So a single-sample difference between two LLM
     routes, prompts or models means nothing. To compare them, use
     `--samples 3` and compare per-question pass counts.
   - **Jev budget.** The deployed chatbot can run a paid Jev routing shadow
     (`GA_ROUTER_JEV_SHADOW=1`; budget `GA_ROUTER_JEV_SHADOW_BUDGET_USD`,
     default $0.05, cumulative over its log). Requests tagged `theory-qa` or
     `probe-*` are not sent to Jev. They log a `skipped_source` row and cost
     nothing (`RoutingTelemetryLog.IsSyntheticTrafficSource` holds the tag
     list), so always pass `--source theory-qa`.
   - **Before the skip is deployed.** Until the deployed release includes the
     skip, every question that reaches the router is billed. On 2026-10-03 that
     was about $0.00015 a call ($0.0136 for 90 calls). A 34-question run costs
     about $0.005, and a `--samples 3` run about $0.015, out of $0.036 left: two
     sampled runs would starve the real-traffic shadow.
   - **Checking after the run.** Look at
     `state/telemetry/routing-jev-shadow/<UTC date>.jsonl` for your run window.
     Your questions must appear only as `skipped_source`. If they appear as
     billed rows, report it, and do not start another sampled run.
4. **Automatic floor (regexes + `tab_check`).** Any `must_not_match` hit,
   missing `must_match` or `tab_check` failure is a fail, whatever you think
   of the rest of the answer. An empty answer or a runner error is a fail
   (fail closed). A `tab_check` failure names the claim, the decoded notes and
   what is wrong. It only fires on confident mismatches: on the 2026-10-03
   stored runs it caught 22 of the 31 tab errors the expert found, with no
   false fail. Its misses are prose, scale fingerings and CAGED tables, so
   step 5 still decodes every diagram.
5. **Expert reading (ceiling).** Read every answer in full. For each concrete
   claim — chord tones and their spelling, scale or mode notes, key
   signatures, intervals, the note at a fret, every tablature or VexTab
   diagram, Roman-numeral function — verify it with the ga MCP tools
   (load them with ToolSearch: chord intervals, key notes, scale notes,
   diatonic chords, key signature) or by an explicit derivation you write
   down. Decode tablature string by string in standard tuning (E A D G B E).
   The ga MCP tools are a second opinion, not ground truth. They are only as
   current as the server's build. On 2026-10-03, a session's server started
   with `dotnet run --no-build` was months old: `ga_chord_intervals` dropped
   the b5 of m7b5, and `get_scale_notes` spelled G minor with A#/D#. The
   current code had both right. Key tools take names such as "Key of A" or
   "Key of Am".

   Always derive spellings from the root by letter counting. When a tool
   disagrees with a derivation you wrote out, trust the derivation and record
   the disagreement under `tool_disagreements` in the snapshot, with a
   `tool_disagreements_note` saying which build the MCP server ran (how it was
   started, and how old it was).
6. **Verdict per answer** (hexavalent):
   - `T` every claim checked and correct;
   - `P` correct answer with a slip that does not change it (e.g. a cosmetic
     voicing detail), named explicitly;
   - `U` could not verify a material claim;
   - `D` probably wrong, evidence incomplete;
   - `F` at least one verified factual error, or the answer does not answer
     the question (wrong scale, only one of two things compared);
   - `C` the answer contradicts itself.
   Tag each non-`T` with a shape: `spelling`, `enharmonic`, `tab`, `concept`,
   `incomplete`, `wrong-skill`, `off-topic`, `infra`.
7. **Snapshot.** Write `state/quality/theory-qa/<UTC date>.json` (envelope:
   `docs/contracts/quality-snapshot.schema.json`):
   - `domain` `theory-qa`, `metric_name` `theory_answer_accuracy`,
     `metric_value` = share of answers judged `T` or `P`;
   - `oracle_status`: `ok` when the accuracy is at least 0.9 and there is no
     `F`/`C`; `warn` otherwise; `error` on infrastructure failure;
   - `summary`: one line with the counts;
   - `problems`: one entry per non-`T` answer, `code` = question id,
     `message` = the wrong claim and the correct fact;
   - extra fields: `release`, `model`, `window_utc`, `by_route`, `by_topic`,
     `verdicts` (id → verdict);
   - with `--samples N`: grade every sample. `metric_value` is then the share
     of all graded answers, and `verdicts` maps each id to its list of verdicts.
   If a snapshot already exists for today, write `<date>-2.json`, never
   overwrite. `state/quality/.snapshot-registry.json` registers this domain:
   check the envelope with `pwsh Scripts/validate-quality-snapshots.ps1
   -Advisory` (no `theory-qa` FAIL line). The registry glob only matches
   `<date>.json`, so a `-2` file is not validated.
8. **Compare** with the previous snapshot: list regressions (was `T`/`P`, now
   `D`/`F`/`C`) and fixes.

## Report back

Briefly: accuracy and counts, errors by route (skill.* vs agents vs
fallback) and by shape, each `F`/`C` with the wrong claim and the correct
fact, regressions and fixes since the last snapshot, the probe window, and
the snapshot path.

## Never

- Commit, push, or open a PR — the snapshot is for a human to commit.
- Restart, redeploy or reconfigure the chatbot, Ollama, or anything shared.
- Grade leniently to keep a streak green: a confident wrong fact is `F` even
  when the rest of the answer is excellent.
- Paste real user questions anywhere; this agent only sends its own set.
