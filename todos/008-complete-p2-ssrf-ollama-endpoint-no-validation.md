---
status: pending
priority: p2
issue_id: "008"
tags: [code-review, security, ssrf, chatbot]
dependencies: ["004"]
---

# P2: SSRF risk — OllamaUrl from IConfiguration with no URI scheme or private-IP validation

## Problem Statement

Phase 2.5 Task 2 makes `OllamaUrl` configurable from `IConfiguration["Ollama:Endpoint"]`. Without validating the URI, any operator who can set environment variables (Docker Compose, Kubernetes) can redirect the narrator's HTTP client to internal services: `http://169.254.169.254/latest/meta-data/` (AWS IMDS), `http://kubernetes.default.svc/`, etc.

## Findings

- `OllamaGroundedNarrator` and `QueryUnderstandingService` will read `Ollama:Endpoint` from config after Phase 2.5 Task 2
- `static readonly HttpClient _httpClient` has no `BaseAddress` restriction
- No scheme validation exists anywhere in the current or proposed codebase
- Standard SSRF attack: set `Ollama__Endpoint=http://internal-metadata-service:80` via env var

## Proposed Solutions

### Option A: Validate URI in OrchestrationServiceExtensions (Recommended)
```csharp
var endpoint = configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
var uri = new Uri(endpoint);
if (uri.Scheme is not ("http" or "https"))
    throw new InvalidOperationException($"Ollama:Endpoint must use http or https. Got: {uri.Scheme}");
// Optionally: reject 169.254.x.x, 10.x, 172.16-31.x unless AllowPrivateNetworks=true
```
- **Effort**: Small (1h)
- **Risk**: Low

### Option B: Accept risk for internal-only deployment
Document that `Ollama:Endpoint` must be set by trusted operators only. Add a warning comment.
- **Effort**: Trivial
- **Risk**: Medium for production cloud deployments

## Recommended Action
*(leave blank for triage)*

## Technical Details
- **Files to update**: `Common/GA.Business.Core.Orchestration/Extensions/OrchestrationServiceExtensions.cs`
- **Phase in plan**: Phase 3 (when writing `AddChatbotOrchestration()`)

## Acceptance Criteria
- [ ] Non-http/https scheme in `Ollama:Endpoint` throws at startup with descriptive message
- [ ] Valid `http://localhost:11434` passes validation
- [ ] Test: `Assert.Throws<InvalidOperationException>(() => AddChatbotOrchestration())` with `ftp://` endpoint

## Work Log
- 2026-03-03: Identified by security-sentinel (P2)

## Resources
- Plan: Phase 2.5 Task 2, Phase 3 `OrchestrationServiceExtensions`
