# Findings — public progression-arpeggio tracer (in progress)

Pre-registration: [preregistration.md](preregistration.md) (written 2026-09-28T01:35:59Z, before any POST).
Oracle and cases were not edited after the runs below.

## Run 1 — 2026-09-28T01:36Z (`runs/20260928T013605Z`)

All six POSTs and both preflight GETs returned **HTTP 502** from Cloudflare. The refutation clause of the study fired: a smaller user-visible failure must be repaired before the tracer can measure answer quality.

Root cause (read-only diagnosis):

- Nothing listened on `localhost:5252` (GaChatbot.Api). GaApi `:5232`, Vite `:5176` and Ollama `:11434` were up; the public root stayed 200.
- The host rebooted on 2026-09-27 at 19:15 local (Start-menu shutdown, event 1074). The chatbot log's last write was 2026-09-26 23:03.
- No boot or logon path starts `:5252`: `Scripts/ga-service-wrapper.ps1` and `Scripts/start-all.ps1` do not mention GaChatbot.Api, and the `GA-Chatbot-5252-Codex` scheduled task has only a one-shot time trigger (last run 2026-09-19).
- That task runs a **Debug build from 2026-09-18** compiled in the main checkout while its working tree was dirty; the public chatbot is therefore not a `main` build.

Action (user-approved): `Start-ScheduledTask GA-Chatbot-5252-Codex`. `/chatbot/api` returned 200 (`ga-chatbot-api` 0.1.0) and `/chatbot/api/chatbot/status` reported `isAvailable`, `providerReachable` and `orchestratorRoundTripOk` all true.

## Run 2 — 2026-09-28T01:41Z (`runs/20260928T014118Z`)

Every POST returned HTTP 200 with a single SSE frame `{"error":"Failed to process message. Please try again."}` and no `[DONE]` (P1 ×3, P2, N1, N2: 2/7 checks each; N2 4/8). Latency is not meaningful (0.46–4.2 s to the error frame).

Root cause from `C:\tmp\ga-chatbot-5252.out.log`: every embedding and chat call to Ollama failed with `error starting llama-server: llama-server binary not found`.

- Ollama 0.34.0 was running elevated (started by `ga-service-wrapper.ps1` at boot).
- At 19:30 the Ollama tray app's auto-updater started a silent 0.34.0 → 0.34.4 upgrade. The installer could not replace `ollama.exe` (locked by the elevated server; `DeleteFile failed; code 5`), aborted, and its rollback left `…\Programs\Ollama\lib\` without the `ollama\` runtime folder.
- The in-memory 0.34.0 server still answers `/api/tags` and `/api/version`, so the chatbot status endpoint reports ready while every model load fails. **The readiness signal does not exercise an embedding or generation.**

The deterministic `ImprovisationSkill` needs no model, yet it was unreachable: the deployed build's `SemanticIntentRouter` logged "query embedding failed; routing falls through to LLM path", and the LLM fallback failed too. `main` already contains a keyword fallback (`IIntent.MatchesWithoutEmbeddings` → `CanHandle`, added 2026-09-23 in #688) that is absent from the deployed build (base `dc2e74cb`).

## Repair — 2026-09-28T02:0xZ

The user ran the elevated repair script (stop the elevated server, silent reinstall of the signed 0.34.4 installer). Afterwards `lib\ollama\llama-server.exe` existed, `/api/version` reported 0.34.4, a `nomic-embed-text` embed returned 768 dimensions (10.1 s cold) and `llama3.2:3b` generated text (30.4 s cold). The Ollama processes still run elevated (the installer relaunched them), so the auto-update lock hazard remains.

## Run 3 — 2026-09-28T02:21Z (`runs/20260928T022145Z`)

| Case | Checks | Route | Routing frame / total | Failures |
|---|---:|---|---|---|
| P1 #1 `Am F C G` | 17/19 | `skill.improvisation` (`orchestrator-skill-semantic`) | 53.2 s / 53.2 s (cold router warm-up after the repair) | F lead `F Ionian` adds Bb; G lead `G Ionian` adds F# |
| P1 #2 | 17/19 | same | 0.22 s | same two |
| P1 #3 | 17/19 | same | 0.15 s | same two |
| P2 `Dm7 G7 Cmaj7` | 16/16 | same | 0.20 s | — |
| N1 `C A Dm G` | 17/19 | same | 0.19 s | Dm lead `D Aeolian` adds Bb; G lead adds F#. Secondary-dominant guard on A passed (A Ionian keeps C#, no natural C). |
| N2 `Hm Q7` | 7/8 | `fallback-direct` (`low-confidence-fallback`) | 0.93 s | uncertainty heuristic failed |

Every prediction in the pre-registration held. Transport, routing frame, terminal `[DONE]`, absence of error frames, per-chord order, arpeggio quality and chord-tone containment all passed for P1, P2 and N1.

N2 answer (LLM fallback), verbatim: "The note Hm Q7 (also known as H) is a perfect 4th above the note Q.To create an arpeggio based on this note, you'll need to find a chord that includes H and its neighboring notes. …" — invented theory for invalid input, contrary to acceptance point 3 of the study. The deterministic "no fabricated per-chord line" check passed only because the LLM did not use the skill's line format; the heuristic check caught it.

## Browser submission — 2026-09-28T02:25Z ([browser-evidence.json](browser-evidence.json))

The public page answered P1 with the same text, but rendered **one** list item (`li` count 1): the four per-chord suggestions and the closing sentence are glued inside the first bullet ("…fits most i chords).- **F** → …"). The raw frames show why: the answer arrives as sentence chunks whose separating whitespace is gone (`…chord).` then `- **F** → …`), and the page concatenates chunks verbatim. The same loss glues ordinary sentences (`…the note Q.To create…`, `…chord).Each arpeggio…`).

Root cause: `SseChunker.SplitIntoChunks` splits on `(?<=[.!?])\s+`, which consumes the whitespace (spaces and newlines) between sentences. It is shared by GaApi's REST SSE and SignalR hub and by GaChatbot.Api.

Tooling notes: `wmux browser click eN` did not dispatch the form submit (a DOM `.click()` on the Send button did); `wmux browser screenshot` timed out.

## Verdict on the tracer

The first slice is **not** yet a correct, complete public answer. Blocking defects, in order of size: (1) the SSE chunker drops inter-sentence whitespace, breaking every multi-sentence answer and the per-chord list; (2) invalid chord input reaches the LLM fallback, which invents theory; (3) per-chord scale advice ignores the progression's key (F Ionian / G Ionian / D Aeolian over a C-major progression). The arpeggios themselves are correct.

## Changes made on this branch

1. **`SseChunker` keeps inter-sentence whitespace.** The split is now zero-width after the whitespace (`(?<=[.!?]\s+)(?=\S)`), so `string.Concat(chunks) == answer`. `SseChunkerTests` pins the invariant with the tracer's own list answer (5 of 7 new tests failed before the change). Every consumer found (the GaChatbot.Api page, ga-client `chatApi.ts`/`chatService.ts`, the SignalR hub) concatenates chunks with no separator, so no client double-spaces. GaApi's REST `WriteSseLineAsync` writes `data: {chunk}\n\n` and ga-client parses one `data:` line per chunk, so that path still cannot carry a newline and drops any line after a blank line inside a chunk — a pre-existing wire-contract defect left for a separate server+client slice; this change does not worsen it and restores its inter-sentence spaces.
2. **Routing-eval input set v0.7** adds `skill.improvisation` with five held-out prompts (none verbatim from the skill's `ExamplePrompts`). Measured locally with the CI configuration (bge-large, min confidence 0.64): before 157/166 in-scope (94.58 %, identical to the committed 2026-06-17 report), after 162/171 (94.74 %); all five new prompts route correctly (confidence 0.84–0.94, margin 0.11–0.17); OOS decline unchanged at 7/8. `Scripts/routing-eval-gate.ps1` passes. Reports: [before](routing-eval-before-v0.6.json), [after](routing-eval-after-v0.7.json).
3. **Prompt corpus:** N1 (`which arpeggio fits C A Dm G`) is an active entry guarding the secondary dominant (`A Ionian`/`A Mixolydian`/`A Phrygian Dominant` required, `A Aeolian` forbidden). N2 (`which arpeggio fits Hm Q7`) is recorded with `skip: true` and the observed fabrication as its reason.

**Live prompt corpus** (`PromptCorpusTests.EveryPrompt_SatisfiesItsInvariants`, in-process host, local Ollama after the repair), run 2026-09-28: this branch 53/70 active prompts pass (N1 passes); unmodified `origin/main` 52/69. The same 17 prompts fail on both (diatonic chords in G/D/F major and A minor, transposition, common tones with null `ga.dsl` grounding, ii-V-I in Bb, E-flat/B-flat relative minor, typo and upper-case diatonic routing); only the first failing substring differs between runs, which points to LLM variance rather than this change. This is far below the last-known-good 98.08 % (2026-07-19) and needs its own diagnosis; it is not caused by this branch.

Not changed here: key-aware scale advice (a musical design decision: infer the key and prefer the diatonic mode, e.g. F Lydian / G Mixolydian / D Dorian in C major) and the invalid-input fallback. Tracked as #744 (key-aware advice) and #745 (invalid-input fallback); the GaApi REST SSE newline contract is #746 and the 17-prompt live corpus regression is #747.

## Open

- Decide separately: a durable `:5252` start path, redeploying GaChatbot.Api from `main`, a readiness probe that performs one real embedding, and removing the elevated `ollama serve` from the boot wrapper.
