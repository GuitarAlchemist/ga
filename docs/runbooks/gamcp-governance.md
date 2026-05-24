---
title: GaMcpServer governance runbook — Agent Governance Toolkit integration
status: living
date: 2026-05-24
related:
  - GaMcpServer/Program.cs
  - GaMcpServer/Policies/governance.yaml
  - GaMcpServer/AlgedonicEmitter.cs
  - Tests/Apps/GaMcpServer.Tests/GovernanceTests.cs
  - docs/contracts/algedonic-signal.schema.json
  - Scripts/algedonic-emit.ps1
---

# GaMcpServer governance runbook

How the .NET MCP server (`GaMcpServer`) is hardened with Microsoft's **Agent Governance Toolkit MCP Extensions** (`Microsoft.AgentGovernance.Extensions.ModelContextProtocol`, public preview). Covers what the policy denies, how to tune it, where signals surface, and what to recheck when the preview package ships GA.

## What it adds

`builder.Services.AddMcpServer()...WithGovernance(...)` in `Program.cs` wraps the existing MCP stack with three layers:

1. **Startup tool-definition scan.** Every `[McpServerTool]`-decorated method is inspected for prompt poisoning, typosquatting, hidden instructions, and schema abuse before the transport opens. Findings land in the logs (we ship `FailOnUnsafeTools = false` so a flagged tool doesn't block the server — tighten to `true` once the first clean run is logged).
2. **Per-call policy enforcement.** `Policies/governance.yaml` is loaded at startup. Every `tools/call` request is evaluated against the rule set; rate-limit hits become `429`-equivalent MCP errors, deny hits short-circuit with a structured failure.
3. **Response sanitization.** Tool results are scanned for `<system>` tags, `*_KEY` / `*_SECRET` patterns, and known exfiltration URL shapes; matches are redacted before the bytes leave the server.

On top of those three the integration also enables the toolkit's **prompt-injection detector** (`EnablePromptInjectionDetection = true`) with an extended **Blocklist** of credential / env-var patterns: `api_key`, `credential`, `${env:`, `$env:`. The YAML condition language only supports `field == 'value'` / `in` / numeric comparisons — substring matching for credential probes lives in the detector, not in the rules.

## Per-rule rationale

| Rule | Action | Why |
|---|---|---|
| `rate-limit-voicing-search` | rate_limit 60/minute | `ga_search_voicings` hits the OPTIC-K mmap index; an agent in a runaway loop can saturate the reader. 60/min leaves headroom for interactive use + CI smoke runs. Telemetry at `state/telemetry/voicing-search/` can tune this later. |
| `rate-limit-voicing-search-by-query` | rate_limit 60/minute | Same as above, for the natural-language entry point. |
| *(injection-detector Blocklist)* | deny (Critical threat) | Inputs containing `api_key`, `credential`, `${env:`, or `$env:` are flagged as Critical-threat prompt injection. Algedonic severity for these maps to `critical` and routes to `on-call`. |
| *(injection-detector defaults)* | deny | Built-in patterns cover "ignore all previous instructions", role-play hijacks, delimiter attacks (`<|im_start|>`, `[INST]`), and SQL injection. Not configurable from YAML; tune via `PromptInjectionConfig` in `Program.cs`. |

## Algedonic signals

When the kernel emits `ToolCallBlocked` or `PolicyViolation`, `AlgedonicEmitter` projects it into the cross-repo VSM inbox at `state/algedonic/inbox.jsonl` (schema: `docs/contracts/algedonic-signal.schema.json`). Severity mapping:

| Event | Severity | Escalation route |
|---|---|---|
| Prompt-injection deny (threat_level=Critical/High) | `critical` | `on-call` (1h unack) |
| YAML deny rule prefixed `deny-credential` / `deny-env` | `critical` | `on-call` (1h unack) |
| Rate-limit hit (`ToolCallBlocked`, no critical threat) | `warn` | `operator` (24h unack) |
| Generic `PolicyViolation` | `warn` | `operator` (24h unack) |
| `PolicyCheck` (every evaluation) | — | not projected (would flood inbox) |

Sample signal captured by `GovernanceTests.Algedonic_emitter_writes_schema_conformant_line`:

```json
{"id":"019e5821-49ce-78d3-8839-723e77073d97","schema":"algedonic-signal-v0.1.0","emitted_at":"2026-05-24T03:57:17Z","repo":"ga","source":"gamcp-governance","severity":"critical","summary":"MCP tool 'ga_parse_chord' blocked by policy '(none)'","details":"event_type=ToolCallBlocked; agent_id=did:mcp:ga-server; policy=(none); event_id=9ebcd21b88e341b1bfcafa9b86c01c93; data={tool_name=ga_parse_chord, allowed=False, action=deny, reason=Prompt injection detected in argument 'chord': DirectOverride (Critical), injection_type=DirectOverride, threat_level=Critical}","evidence_url":null,"affected_artifacts":["GaMcpServer/Policies/governance.yaml"],"ttl_hours":24,"escalation":{"on_unack_after_hours":1,"route_to":"on-call"},"ack":{"acked":false,"acked_by":null,"acked_at":null,"resolution":null},"supersedes":[]}
```

The Harness dashboard reads this inbox via its existing algedonic projector — no changes to the dashboard were needed for this integration.

## Editing the YAML

The policy file is copied to the GaMcpServer binary directory at build time (`CopyToOutputDirectory="PreserveNewest"` on `Policies\governance.yaml`). The toolkit reads it once when `WithGovernance(...)` materializes the options.

- **Live reload is NOT supported.** Editing `Policies/governance.yaml` requires a `GaMcpServer` restart. (The toolkit's `GovernanceKernel.LoadPolicy(...)` API exists but the MCP extension wraps it during DI build, not on a file watcher.)
- **Condition syntax** supports: `field == 'value'`, `field != 'value'`, `field > 10`, `field in list_field`, `bool_field`, plus compound `and` / `or`. Available context fields are `tool_name`, `agent_did`, and any tool-call arg values.
- **No substring or regex** in conditions. For pattern matching, extend `PromptInjectionConfig.Blocklist` or `.CustomPatterns` in `Program.cs`.
- **Conflict resolution** is `PriorityFirstMatch` (highest `priority:` wins on the first match). Ties break by declaration order.
- **Default action** when no rule matches is `allow` (set via `default_action: allow` at the top level — note: **not** `defaults: {action: allow}`, which the YAML deserializer silently ignores).

## Public-preview upgrade checklist

`Microsoft.AgentGovernance.Extensions.ModelContextProtocol` is at **3.7.0 public preview** as of 2026-05-24. The Microsoft team has explicitly said the API may shift before GA. When the package version bumps, recheck:

- [ ] `IMcpServerBuilder.WithGovernance(...)` signature still takes `Action<McpGovernanceOptions>`.
- [ ] `McpGovernanceOptions` properties used in `Program.cs` still exist: `PolicyPaths`, `ServerName`, `RequireAuthenticatedAgentId`, `DefaultAgentId`, `ScanToolsOnStartup`, `FailOnUnsafeTools`, `SanitizeResponses`, `EnablePromptInjectionDetection`, `PromptInjectionConfig`.
- [ ] `GovernanceKernel` is still resolvable from `IServiceProvider` after `WithGovernance(...)` runs — `Program.cs` does `app.Services.GetService<GovernanceKernel>()` for the audit hook.
- [ ] `GovernanceEvent.SessionId` is still `required`; the `AlgedonicEmitter` reads `Data["tool_name"]`, `Data["threat_level"]`, `Data["injection_type"]`.
- [ ] Policy YAML still uses `apiVersion: governance.toolkit/v1` and `default_action`.
- [ ] `DetectionConfig.Blocklist` still exists (the credential-probe defense depends on it).

If any check fails, update the integration **and** the `GovernanceTests` so the regression surface stays honest.

## Smoke test

After any change:

```bash
cd Tests/Apps/GaMcpServer.Tests
dotnet test --nologo
```

Expected: 7 passed (`Allowed_tool_call_passes_through`, `Credential_probe_in_input_is_denied`, `Env_substitution_pattern_is_denied`, `Rate_limiter_trips_after_threshold`, `Algedonic_emitter_writes_schema_conformant_line`, `Algedonic_emitter_maps_rate_limit_event_to_warn`, `Algedonic_emitter_generates_uuidv7_in_emission_order`).

End-to-end: launch `GaMcpServer` from Claude Code's MCP config, call any tool with `api_key` in an arg value, then check `state/algedonic/inbox.jsonl` for a new `critical`-severity line whose source is `gamcp-governance`.

## What this PR does NOT cover

- **GaChatbot.Api governance.** This is the first .NET-side governance layer; the same pattern (`AddMcpServer` → `WithGovernance`) doesn't apply to the chatbot API directly because it's not an MCP server. The Agent Governance Toolkit has a separate ASP.NET middleware example that would be the right model — left as future work.
- **Tightening `FailOnUnsafeTools` to `true`.** Run the server once, scan logs for any startup scanner findings on the existing tool surface, then flip the switch in a follow-up.
- **Audit-log persistence.** The toolkit ships an `AuditLogger` with hash-chained entries — we only wire the in-memory `OnEvent` projection today. Persistence is a separate decision (write to disk? to the algedonic inbox itself? to Aspire telemetry?).
