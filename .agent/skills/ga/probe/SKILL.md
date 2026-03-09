---
name: "GA Probe"
description: "Talk to the GA chatbot agents, probe routing decisions, compare responses across TheoryAgent / TabAgent / CriticAgent. Use when testing agent behaviour, verifying routing, or exploring chatbot responses programmatically."
---

# /ga probe — GA Chatbot Probe

Use this sub-command when you need to **test agent behaviour**, **compare routing decisions**, or **get music theory answers** from the live GA chatbot (Ollama-backed, SemanticRouter-dispatched).

## When to Use

- Testing that `SemanticRouter` picks the right agent for a given query
- Comparing how `TheoryAgent` vs `CriticAgent` answer the same question
- Getting a real chatbot answer as context while writing code
- Verifying a newly deployed agent change works end-to-end
- Confirming orchestrator-level skills short-circuit routing (`routingMethod: "orchestrator-skill"`)

## Single Agent Query

```bash
curl -sk -X POST https://localhost:7001/api/ga/eval \
  -H "Content-Type: application/json" \
  -d "$(jq -n --arg q "What intervals make up a Cmaj7 chord?" \
       '{script: ("invoke \"agent.theoryAgent\" (Map.ofList [\"question\", box " + ($q | @json) + "])")}')"
```

Or in a GAL script:
```fsharp
invoke "agent.theoryAgent" (Map.ofList ["question", box "What intervals make up a Cmaj7 chord?"])
```

The `SemanticRouter` inside the GA API picks the best specialist (Theory / Tab / Critic / Composer) based on embedding similarity. The agent closure does **not** force a specific agent — routing still happens at the API level.

## Fan-Out: Compare Multiple Agents

To send the same question to multiple agent closures in parallel and collect all answers:

```fsharp
invoke "agent.fanOut" (Map.ofList [
    "question",   box "Analyse the progression Am - F - C - G"
    "agentNames", box [| "agent.theoryAgent"; "agent.criticAgent" |]
])
```

Returns `Map<string, string>` — agent name → response. Display side-by-side.

## Direct Chatbot Endpoint (no GAL)

For debugging routing metadata (agent selected, confidence, routing method):

```bash
curl -sk -X POST https://localhost:7001/api/chatbot/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Show me fingering for an open G chord", "useSemanticSearch": true}' \
  | python -m json.tool
```

Response shape:
```json
{
  "naturalLanguageAnswer": "...",
  "candidates": [...],
  "routing": {
    "agentId": "tab-agent",
    "confidence": 0.91,
    "routingMethod": "embedding"
  }
}
```

Key fields: `routing.agentId`, `routing.confidence`, `routing.routingMethod` (`embedding` | `llm` | `keyword` | `orchestrator-skill`).

## Probing Routing Decisions

### Does a query route to the expected agent?

```fsharp
// Ask via the chatbot endpoint to get routing metadata
let probe question =
    invoke "io.httpPost" (Map.ofList [
        "url",  box "https://localhost:7001/api/chatbot/chat"
        "body", box (sprintf """{"message":"%s","useSemanticSearch":true}""" question)
    ])
```

Then inspect `routing.agentId` to verify.

### Verifying orchestrator-skill short-circuit

For queries handled by `ScaleInfoSkill` or `KeyIdentificationSkill`, the response should show `routingMethod: "orchestrator-skill"` — meaning no LLM routing or embedding lookup was performed:

```bash
curl -sk -X POST https://localhost:7001/api/chatbot/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "What notes are in C major?"}' | jq '.routing'
# Expected: { "routingMethod": "orchestrator-skill", "agentId": "skill.ScaleInfo", ... }
```

### Routing confidence regression test

When changing `SemanticRouter` weights or embeddings, run these known queries and record confidence scores:

| Query type | Expected agent | Min confidence |
|---|---|---|
| "What is a tritone substitution?" | `theory-agent` | 0.80 |
| "Show me tabs for an A minor pentatonic lick" | `tab-agent` | 0.75 |
| "Critique this progression: I IV V vi" | `critic-agent` | 0.70 |
| "What notes are in G major?" | `orchestrator-skill` | 1.0 (domain) |
| "What key am I in if I play Am F C G?" | `orchestrator-skill` | varies |

Compare before/after to detect regressions.

## Batch Question Comparison

```fsharp
let questions = [|
    "What is a tritone substitution?"
    "How do I play a B7 on guitar?"
    "Is Dm → G → C a good progression?"
|]

questions |> Array.iter (fun q ->
    let result = invoke "agent.theoryAgent" (Map.ofList ["question", box q])
    printfn "Q: %s\nA: %A\n---" q result)
```

## Checking Agent Status

```bash
curl -sk https://localhost:7001/api/chatbot/status
```

Returns whether Ollama is reachable and the model is loaded. Check this first if agent closures are timing out.

## Interpreting Agent Responses

- **TheoryAgent**: deep explanations with interval analysis, historical context
- **TabAgent**: VexTab or ASCII tab output, fingering instructions
- **CriticAgent**: voice-leading critique, cadence analysis, harmonic function labels
- **ComposerAgent**: melodic/progression suggestions
- **ScaleInfoSkill** (orchestrator): zero-LLM scale note lookup, relative key, key signature
- **KeyIdentificationSkill** (orchestrator): domain-scored key identification with LLM explanation

If the routing is wrong (e.g. a tab question answered by TheoryAgent), check:
1. `routing.routingMethod` — if `keyword`, the embedding fallback triggered
2. `routing.confidence` — below 0.6 often means ambiguous query
3. Consider rephrasing or adding explicit hints to the query
