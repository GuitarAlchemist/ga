---
title: "Two casing-variant DI extension methods drift apart silently — `AddGuitarAlchemistAi` vs `AddGuitarAlchemistAI`"
date: 2026-05-07
problem_type: "architecture"
component: "GA.Business.ML.Extensions / DI composition"
symptoms:
  - "Service works in some entry-point apps, breaks in others, with no compile error or runtime warning at boot"
  - "First chatbot endpoint call returns HTTP 500: `No service for type 'GA.Business.ML.Extensions.IChatClientFactory' has been registered`"
  - "Stack trace points at SkillMdPlugin.Register&gt;b__0 inside `IEnumerable<IOrchestratorSkill>` resolution"
  - "GaApi /api/health is green, /api/chatbot/status is 500"
  - "The fix that 'works' is to add the same registration to one helper, papering over the duplicated composition root"
tags:
  - "csharp"
  - "dependency-injection"
  - "extension-methods"
  - "composition-root"
  - "naming-conventions"
severity: "high"
related_docs:
  - "docs/solutions/runtime-errors/2026-05-07-mcp-withtools-overload-resolution-trap.md"
related_prs:
  - "#150 (closed in favour of #151) — initial single-line fix"
  - "#151 — the comprehensive review-driven sweep that found the underlying smell"
---

# Two `AddGuitarAlchemistA[iI]` extension methods registering different DI graphs

## Symptoms

`MlServiceCollectionExtensions.AddGuitarAlchemistAI` and `AiServiceExtensions.AddGuitarAlchemistAi` are not casing typos. They are two parallel composition roots for the same module — the lowercase one uses MEAI patterns and registers `IChatClientFactory`, the uppercase one is the legacy graph and historically did not.

GaApi calls the uppercase variant via `AddAiServices(IConfiguration)` → `services.AddGuitarAlchemistAI()`. Other entry points (`GaChatbot.Api`, `GaChatbotCli`, `GA.AI.Service`, plus tests) call the lowercase variant via `AddGuitarAlchemistAi(IConfiguration)`.

When `SkillMdPlugin.Register` started lazily resolving `IChatClientFactory` (Phase 1 of the chatbot migration), only the lowercase callers worked. GaApi worked too, **as long as `skills/` was empty** — the foreach loop in `SkillMdPlugin.Register` had nothing to register, so no `IOrchestratorSkill` factory ever asked for `IChatClientFactory`.

As soon as `skills/` accumulated SKILL.md files (cumulatively across PRs #124, #126, #142, #143, #146 — none of which alone tipped the threshold), every chatbot endpoint that resolved `IEnumerable<IOrchestratorSkill>` started 500ing during DI activation:

```
System.InvalidOperationException: No service for type
'GA.Business.ML.Extensions.IChatClientFactory' has been registered.
  at SkillMdPlugin.<>c__DisplayClass5_0.<Register>b__0(IServiceProvider sp)
  at ... VisitIEnumerable ...
  at GaApi.Controllers.ChatbotController.GetStatus
```

PR #150 added the same `services.TryAddSingleton<IChatClientFactory, DefaultChatClientFactory>();` line to the uppercase helper. That fixed *the symptom* — the underlying drift remains.

## Root cause: parallel composition roots in the same module

Two extension methods with case-only-different names register fundamentally different graphs. Neither is documented as deprecated, neither documents the relationship, and the project graph routes different consumers through different methods:

```
AddGuitarAlchemistAi  ← GaChatbot.Api, GaChatbotCli, GA.AI.Service, tests
AddGuitarAlchemistAI  ← GaApi (the one with the public demo + the most callers)
```

Each new dependency added to one helper must be added to the other. There is no compile-time, build-time, or runtime gate enforcing parity. The forward-drift is silent until a new code path lights up the missing registration.

## Fix layer 1 (already shipped — PR #150 line in #151)

Add the missing `IChatClientFactory` registration to the uppercase helper:

```csharp
// Common/GA.Business.ML/Extensions/ServiceCollectionExtensions.cs
services.TryAddSingleton<IChatClientFactory, DefaultChatClientFactory>();
```

Plus a regression test that asserts *this specific symptom* is gone:

```csharp
// Tests/Common/GA.Business.ML.Tests/Unit/AddGuitarAlchemistAiRegistrationTests.cs
[Test]
public void AddGuitarAlchemistAI_RegistersIChatClientFactory()
{
    var services = new ServiceCollection();
    services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
    services.AddSingleton<IChatClient>(_ => new Mock<IChatClient>().Object);
    services.AddGuitarAlchemistAI();

    using var provider = services.BuildServiceProvider();
    Assert.That(provider.GetService<IChatClientFactory>(), Is.Not.Null);
}
```

That test pins one symptom. It does not pin the next divergence — for that you need the next layer.

## Fix layer 2 (deferred — tracked as arch-F1)

Collapse to one canonical `AddGuitarAlchemistAi(IConfiguration)`. Have the legacy uppercase method delegate to it (or mark `[Obsolete]`). Add a parity test:

```csharp
[Test]
public void Both_AddGuitarAlchemist_Helpers_RegisterIdenticalServiceTypes()
{
    var lower = new ServiceCollection();
    var upper = new ServiceCollection();
    SeedDeps(lower); SeedDeps(upper);

    lower.AddGuitarAlchemistAi(BlankConfig());
    upper.AddGuitarAlchemistAI();

    var lowerTypes = lower.Select(d => d.ServiceType.FullName).ToHashSet();
    var upperTypes = upper.Select(d => d.ServiceType.FullName).ToHashSet();

    var onlyInLower = lowerTypes.Except(upperTypes).ToList();
    var onlyInUpper = upperTypes.Except(lowerTypes).ToList();

    Assert.That(onlyInLower, Is.Empty,
        $"lowercase registers types upper doesn't: {string.Join(", ", onlyInLower)}");
    Assert.That(onlyInUpper, Is.Empty,
        $"uppercase registers types lower doesn't: {string.Join(", ", onlyInUpper)}");
}
```

A test that fails when the two drift catches the *next* missing type, not just `IChatClientFactory`.

## Detection rule of thumb

When two extension methods in the same module differ only by casing:

1. Treat it as a smell, not a style choice. Either consolidate or document the relationship explicitly.
2. Write a parity test. If you can't (because they intentionally diverge), document the divergence.
3. Audit every `services.TryAdd*` / `services.Add*` line in both. If the lists are not identical, you have latent breakage waiting for a new caller.

## Pattern: the latency between commit and breakage matters

The PR that introduced `IChatClientFactory` shipped weeks before the symptom appeared. The threshold was crossed by an unrelated PR that added a SKILL.md with triggers. Latent DI breakage binds to the time-of-call, not the time-of-commit — and `IServiceProvider.GetService<IEnumerable<T>>` is a well-known fault amplifier because every registration in the iteration is resolved together. When you add a new factory closure to a `services.AddSingleton<T>(sp => ...)`, ask: which existing IEnumerable resolution sites will start calling this on first request? That's where the silent drift surfaces.
