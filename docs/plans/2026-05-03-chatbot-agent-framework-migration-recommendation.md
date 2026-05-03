---
date: 2026-05-03
status: recommendation
reversibility: spike-first; no production migration should happen before measured parity
owners: needs assignment
audience: Claude Code / Codex / GA maintainers
---

# GA chatbot migration recommendation for Claude Code

This document gives Claude Code a concrete migration brief for modernizing the
GA chatbot without turning the codebase into a vendor-specific agent demo.

The recommendation is to use **Microsoft Agent Framework** as the strategic
agent/workflow layer, keep **Microsoft.Extensions.AI** as the provider-neutral
model/tool contract, and use the official **Anthropic C# SDK** only as a Claude
provider adapter.

Do not replace the current orchestrator in one pass.

## Current state

The chatbot already has several important abstractions that should be preserved:

- `Common/GA.Business.ML/Agents/IOrchestratorSkill.cs`
  - deterministic pre-router skill contract
  - `CanHandle(string)` fast path
  - `ExecuteAsync(...)` returns GA's `AgentResponse`
  - first match wins

- `Common/GA.Business.Core.Orchestration/Services/ProductionOrchestrator.cs`
  - invokes `IOrchestratorSkill` before the full LLM route
  - preserves low-latency answers for deterministic theory/voicing tasks
  - owns hook/tracing integration around skill execution

- `Common/GA.Business.ML/Agents/Plugins/InProcessMcpToolsProvider.cs`
  - starts an in-process MCP server
  - exposes MCP tools as `Microsoft.Extensions.AI.AIFunction`
  - is already the right vendor-neutral bridge for LLM tool calling

- `Common/GA.Business.ML/Agents/Skills/SkillMdDrivenSkill.cs`
  - currently constructs `AnthropicClient`
  - immediately converts it to `IChatClient`
  - uses `UseFunctionInvocation()` and GA MCP tools
  - this is directionally right, but construction should move behind a provider factory

- `Apps/ga-server/GaApi/GaApi.csproj`
  - already references `Microsoft.Agents.AI`
  - this suggests Agent Framework has already been considered, but it is not yet the
    canonical chatbot orchestration contract

## Recommendation

Adopt Microsoft Agent Framework gradually, using a compatibility spike first.

The desired long-term shape is:

```text
UI / API / SignalR
  -> GA chatbot application service
    -> deterministic IOrchestratorSkill fast path
    -> Microsoft Agent Framework agent/workflow path for multi-step work
      -> AgentSkillsProvider for portable SKILL.md capabilities
      -> Microsoft.Extensions.AI IChatClient
        -> Anthropic / Ollama / OpenAI / Foundry provider adapters
      -> AIFunction tools from GA MCP provider
      -> A2A hosting/client only at the boundary
```

The boundary rule is strict:

- GA application contracts must use `IChatClient`, `AIFunction`, `AgentResponse`,
  GA trace DTOs, or Agent Framework abstractions.
- Provider SDK DTOs must not leak into `IOrchestratorSkill`,
  `ProductionOrchestrator`, API responses, trace payloads, or UI contracts.
- Anthropic-specific types belong only in a provider adapter/factory.
- Agent Framework should orchestrate agents/workflows, not replace simple
  deterministic functions.

## Why Microsoft Agent Framework

Microsoft Agent Framework is the more modern Microsoft agent stack. It combines
AutoGen-style agent abstractions with Semantic Kernel enterprise features and
adds graph workflows, sessions, middleware, telemetry, MCP, A2A, and DevUI.

Use it for:

- multi-agent or multi-step chatbot behavior
- QA architect / tribunal workflows
- richer agentic traces
- explicit graph workflows with checkpoints
- A2A exposure and A2A clients
- future DevUI-based local testing

Do not use it for:

- one-shot deterministic music theory answers
- simple chord/scale parsing
- LLM-as-classifier dispatch (per-call prompt to a model asking "which intent
  is this?"). Routing decisions must not pay LLM round-trip latency.
- making every GA domain operation an autonomous agent

Microsoft's own guidance says: if a function can handle the task, use a function
instead of an AI agent. That maps directly to GA's deterministic music logic.

### Routing classifiers: embedding similarity is a function, not an agent

The "no model-driven routing" rule above is about **LLM agents** doing per-call
intent classification. **Embedding-based routing is in scope** and should
replace ad-hoc keyword/regex classifiers (`KeywordAlgebraPromptClassifier`,
`IsAskingForOptimization`, individual `IOrchestratorSkill.CanHandle` regexes).

Why embedding routing counts as "function over agent":

- Cosine similarity is a closed-form computation over fixed vectors.
- Intent embeddings are computed **once at startup** from each intent's
  `Description` + `ExamplePrompts`, then cached for the process lifetime.
- Per query: one embedding (~5 ms locally with `nomic-embed-text`) + N dot
  products. Deterministic, side-effect-free, fully auditable — the trace
  records which example matched and at what score.
- No LLM round-trip on the routing path.

This pattern is the canonical replacement for string-matching dispatch in GA:

- Each intent (algebra, voicing-search, modes, scale-info, transpose,
  tab-optimize, tab-analyze, …) registers as an `IIntent { Id, Description,
  ExamplePrompts }` in the DI container.
- A single `SemanticIntentRouter` consumes all `IIntent` registrations plus an
  `IEmbeddingGenerator<string, Embedding<float>>` and dispatches by cosine
  similarity. Top match above threshold (≈ 0.65–0.75) wins; below threshold
  falls through to the LLM agent path (which is still rare and bounded).
- Skills keep their **deterministic execution** (`ExecuteAsync` stays as is).
  Only **dispatch** moves from `CanHandle` regex → embedding similarity.

Skills that previously held string-matching `CanHandle` should expose
`ExamplePrompts` and stub `CanHandle` to `false` so they participate in
semantic dispatch only. Legacy `CanHandle` paths can stay as fallback
during migration but the goal is single-source-of-truth intent registration.

## C# equivalent of Spring AI generic skills

The C# equivalent of Spring AI's generic Agent Skills pattern is
**Microsoft Agent Framework `AgentSkillsProvider`**.

Use this instead of maintaining a long-term custom `SKILL.md` loader.

Spring AI generic skills and Agent Framework skills line up almost directly:

| Spring AI generic skills | C# / Microsoft Agent Framework |
|---|---|
| `SKILL.md` folders | `AgentSkillsProvider` file-based skills |
| YAML frontmatter | `name`, `description`, `license`, `compatibility`, `metadata`, `allowed-tools` |
| startup discovery | provider scans skill directories |
| progressive disclosure | advertise skill, load body, read resources, run scripts |
| `SkillsTool` | built-in `load_skill` tool |
| `FileSystemTools.Read` | built-in `read_skill_resource` tool |
| `ShellTools.Bash` | `run_skill_script` with an explicit script runner |
| provider portability | Agent Framework + `Microsoft.Extensions.AI` providers |

The important difference is operational: script execution must be explicitly
enabled and treated as privileged. GA should start with read-only skills and
resources, then add scripts only with approval, sandboxing, timeouts, and trace
logging.

Recommended GA skill layout:

```text
skills/
  chord-voicing-review/
    SKILL.md
    references/
      voicing-quality.md
      fretboard-ergonomics.md
    assets/
      examples.json
  qa-architect/
    SKILL.md
    references/
      qa-verdict-contract.md
```

Example C# shape:

```csharp
using Microsoft.Agents.AI;

var skillsProvider = new AgentSkillsProvider(
    Path.Combine(AppContext.BaseDirectory, "skills"));

AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    Name = "GaSkillsAgent",
    ChatOptions = new()
    {
        Instructions = "You are the Guitar Alchemist assistant."
    },
    AIContextProviders = [skillsProvider],
});
```

Only add script execution after the read-only path is proven:

```csharp
var skillsProvider = new AgentSkillsProvider(
    Path.Combine(AppContext.BaseDirectory, "skills"),
    SubprocessScriptRunner.RunAsync);
```

For production, do not use a raw subprocess runner without wrapping it in GA
policy:

- allow-list script names and extensions
- reject scripts outside the skill directory
- pass arguments as structured JSON only
- set wall-clock timeout
- set output size limits
- redact secrets from logs and traces
- record script name, arguments, exit code, duration, and output summary
- require human approval for mutating scripts

`Common/GA.Business.ML/Agents/Skills/SkillMdDrivenSkill.cs` is now best viewed
as a bridge/prototype. It already proves the rough pattern: a `SKILL.md` body,
an `IChatClient`, `UseFunctionInvocation()`, and GA MCP tools. The migration
should move this onto `AgentSkillsProvider` where possible, while preserving the
GA deterministic fast path.

## Why keep Microsoft.Extensions.AI

`Microsoft.Extensions.AI` is the right lowest common denominator for providers
and tools in this repo.

It gives GA:

- provider-neutral `IChatClient`
- provider-neutral `AIFunction`
- `FunctionInvokingChatClient` / `UseFunctionInvocation`
- compatibility with Ollama, OpenAI, Anthropic, and MCP tools
- a stable testing seam for fake chat clients

Claude Code should not introduce direct calls to Anthropic, OpenAI, Ollama, or
Foundry throughout the agent code. Add provider-specific code only behind a
factory that returns `IChatClient`.

## Anthropic SDK guidance

Use `https://github.com/anthropics/anthropic-sdk-csharp` when Claude-specific
behavior is required. It is now the official C# SDK package under `Anthropic`
version 10+.

Allowed uses:

- provider adapter construction
- Claude model selection
- Claude-specific timeouts/retries
- Claude streaming behavior if `IChatClient` cannot express a required feature
- Claude vision or prompt-caching experiments behind capability checks

Not allowed:

- Anthropic DTOs in API contracts
- Anthropic DTOs in trace contracts
- Anthropic DTOs in `AgentResponse`
- Anthropic exceptions escaping user-facing endpoints
- direct `new AnthropicClient` calls outside provider setup code

Important risk: the Anthropic C# SDK is official but still documented as beta.
Pin package versions and isolate usage so a minor SDK break does not ripple
through GA.

## Proposed migration phases

### Phase 0 - freeze the baseline

Before any migration:

- run the chatbot smoke tests for deterministic skill queries
- capture latency for deterministic skill answers vs full LLM answers
- capture one detailed trace for a deterministic skill and one full-agent answer
- identify current package versions for `Microsoft.Agents.AI`, `Anthropic`,
  `Microsoft.Extensions.AI`, and `ModelContextProtocol`

No new framework work should ship without this baseline.

### Phase 1 - provider factory cleanup

Goal: eliminate direct vendor construction from skills.

Add a small provider-neutral factory, for example:

```csharp
public interface IChatClientFactory
{
    IChatClient Create(string purpose);
}
```

Initial purposes can be:

- `default`
- `skill-md`
- `qa-architect`
- `fast-local`

Move the `AnthropicClient.AsIChatClient(...).AsBuilder().UseFunctionInvocation().Build()`
construction out of `SkillMdDrivenSkill` and into the factory.

Acceptance criteria:

- `SkillMdDrivenSkill` depends on `IChatClient` or `IChatClientFactory`, not
  `AnthropicClient`
- unit tests can inject a fake `IChatClient`
- config can switch `skill-md` between Anthropic and local/Ollama where supported
- no Anthropic DTO leaves the factory

### Phase 2 - Agent Skills provider migration

Goal: replace the custom long-term `SKILL.md` mechanism with Agent Framework's
standard skills provider where it fits.

Do this before migrating broader orchestration. Skills are the smallest useful
place to validate the modern C# equivalent of Spring AI generic skills.

Tasks:

- create a `skills/` root for GA-owned portable skills
- move or mirror one existing `SKILL.md` capability into that root
- wire `AgentSkillsProvider` into an experimental Agent Framework agent
- keep scripts disabled for the first pass
- verify `load_skill` and `read_skill_resource` behavior
- map skill output back into the existing chatbot response shape
- keep `SkillMdDrivenSkill` available until parity is proven

Acceptance criteria:

- one file-based skill is discovered from disk
- the agent advertises the skill without loading the full body upfront
- the agent can load the skill body on demand
- the agent can read a bundled reference file
- no script execution is enabled by default
- the same skill can run against at least two configured providers where tool
  support allows it
- tests cover malformed frontmatter, missing resource, and successful skill load

#### Porting policy: catalog vs. computation skills

Not every existing `IOrchestratorSkill` is a good SKILL.md candidate. Apply
this rule when deciding whether to port a skill:

- **Catalog skills** (pure data, no domain types touched) → port to SKILL.md.
  The body becomes the system prompt; the LLM reproduces the catalog. Hot-
  editable, shareable across providers, no rebuild required.
  - Examples: `BeginnerChordsSkill` (8 open-position chord diagrams),
    `ProgressionMoodSkill` (5 darken + 4 brighten technique catalog).
  - Both ported as canaries in PRs #74 and (this PR).

- **Computation skills** (call into `GA.Domain.*`, run regex parsing, do
  pitch-class arithmetic, etc.) → keep in C#. Porting these to SKILL.md
  would force the LLM to recompute results from training data, losing the
  determinism that made them deterministic skills in the first place.
  - Examples: `IntervalSkill` (`note.GetInterval()`), `ScaleInfoSkill`
    (`Key.Items`), `ChordInfoSkill`, `KeyIdentificationSkill`,
    `ProgressionCompletionSkill` (hybrid — keeps domain compute + LLM phrasing),
    `FretSpanSkill`, `ModesSkill`, `ChordSubstitutionSkill`.
  - Migration path for these: expose the domain operation as an MCP tool, then
    a thin SKILL.md can declare `allowed-tools` and let the LLM call the
    deterministic backend. That's a separate workstream — do not block the
    catalog migration on it.

Net effect: of GA's ten current `IOrchestratorSkill` implementations, only
**two** (BeginnerChords, ProgressionMood) are pure catalogs. The remaining
eight stay C# until the MCP-tool exposure workstream is scoped.

### Phase 3 - Agent Framework spike

Goal: prove Agent Framework improves orchestration without regressing the fast path.

Create a small experimental adapter around the existing chatbot path:

- wrap the full chatbot path as an Agent Framework `AIAgent`
- attach `AgentSkillsProvider` for portable skills
- expose one existing deterministic skill as an `AIFunction`
- connect existing MCP tools
- verify streaming behavior
- verify trace richness
- verify cancellation/timeout behavior
- verify local provider support

This spike should be isolated. Do not rewrite `ProductionOrchestrator` yet.

Suggested location:

- `Common/GA.Business.ML/Agents/AgentFramework/`
- or `Apps/GaChatbot.AgentFrameworkSpike/` if the dependency graph gets messy

Acceptance criteria:

- deterministic fast-path answers remain faster than model/tool routing
- Agent Framework can call GA MCP tools
- Agent Framework output can be mapped back to existing chatbot response DTOs
- no UI contract change is required
- package versions are pinned
- tests cover at least one success, one tool failure, and cancellation

### Phase 4 - A2A boundary

Goal: expose the chatbot as an A2A agent without changing internal contracts.

Use Agent Framework A2A hosting at the edge:

- expose one GA chatbot agent card
- support streaming where possible
- map A2A requests into the existing chatbot application service
- map chatbot traces into A2A-visible task/run metadata where appropriate

Acceptance criteria:

- A2A client can discover the agent card
- A2A client can run a basic query
- A2A client can run a long-running query or receive a continuation/background
  response if supported
- local chatbot UI still works unchanged

### Phase 5 - workflow migration for agentic work only

Only after Phases 1-4 succeed, move multi-step agentic flows into Agent
Framework workflows.

Good candidates:

- QA architect / tribunal
- cross-repo review orchestration
- multi-agent critique loops
- long-running evaluation workflows
- workflows requiring checkpointing or human approval

Bad candidates:

- chord info
- scale info
- modes
- fret span
- deterministic voicing lookups

## Trace requirements

The migration must improve, not weaken, agentic trace detail.

Minimum trace fields:

- trace id
- run id
- protocol tags
- provider
- model
- agent/workflow id
- selected skill or tool
- routing method
- tool call names
- tool call arguments after secret redaction
- tool call duration
- tool result summary
- retry count
- token usage where available
- cancellation/timeout reason
- final response length

Provider-specific details may be recorded, but only inside a provider metadata
bag that the UI can safely ignore.

## Test requirements

Claude Code should add or preserve tests for:

- provider factory selection by config
- fake `IChatClient` injection
- `SkillMdDrivenSkill` without Anthropic API key when a fake client is supplied
- `AgentSkillsProvider` discovers one file-based GA skill
- `AgentSkillsProvider` rejects malformed skill frontmatter
- read-only skill resources can be loaded on demand
- scripts are disabled by default
- Anthropic provider config failure with a safe error
- MCP tool list propagation into `ChatOptions`
- cancellation propagation
- deterministic skills bypassing Agent Framework
- Agent Framework spike maps output into existing chatbot DTOs
- A2A endpoint smoke test if Phase 3 is attempted

Do not claim migration success unless these pass:

```powershell
dotnet build AllProjects.slnx -c Debug
dotnet test AllProjects.slnx
```

For frontend-facing changes:

```powershell
Push-Location ReactComponents/ga-react-components
npm run build
npm run lint
Pop-Location
```

## Non-goals

- Do not replace every `IOrchestratorSkill` with an Agent Framework agent.
- Do not make Claude the only supported provider.
- Do not move music-domain computation into prompts.
- Do not keep custom `SKILL.md` loading as the permanent path if
  `AgentSkillsProvider` covers the use case.
- Do not enable skill scripts in production without sandboxing, approval policy,
  resource limits, and audit traces.
- Do not introduce a second chatbot response contract.
- Do not expose raw provider errors to users.
- Do not make DevUI production infrastructure.

## Decision summary

Claude Code should proceed as follows:

1. Preserve GA deterministic fast paths.
2. Centralize model/provider construction behind `IChatClient`.
3. Use Agent Framework `AgentSkillsProvider` for portable file-based skills.
4. Use Anthropic SDK only inside the Anthropic provider adapter.
5. Spike Microsoft Agent Framework for multi-step agentic orchestration.
6. Use Agent Framework A2A at the boundary if the spike proves clean.
7. Migrate only high-value multi-step workflows after parity and tests.

This gives GA the modern Microsoft agent stack while preserving provider
choice and keeping deterministic music logic fast, testable, and inspectable.

## Official references checked

- Microsoft Agent Framework overview:
  `https://learn.microsoft.com/en-us/agent-framework/overview/`
- Microsoft Agent Framework workflows:
  `https://learn.microsoft.com/en-us/agent-framework/workflows/`
- Microsoft Agent Framework Agent Skills:
  `https://learn.microsoft.com/en-us/agent-framework/agents/skills`
- Microsoft Agent Framework tools:
  `https://learn.microsoft.com/en-us/agent-framework/agents/tools/`
- Microsoft Agent Framework A2A:
  `https://learn.microsoft.com/en-us/agent-framework/integrations/a2a`
- Anthropic C# SDK:
  `https://platform.claude.com/docs/en/api/sdks/csharp`
- Anthropic SDK repository:
  `https://github.com/anthropics/anthropic-sdk-csharp`
- Microsoft.Extensions.AI tool calling:
  `https://learn.microsoft.com/en-us/dotnet/ai/conceptual/ai-tools`
- Spring AI generic Agent Skills comparison point:
  `https://spring.io/blog/2026/01/13/spring-ai-generic-agent-skills/`
