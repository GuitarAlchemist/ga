# Track: Project Consolidation & Modernization

## Overview
Status: Active
Owner: Antigravity
Description: Consolidate service registration, stabilize namespaces, modernized code, and complete the microservices transition for Guitar Alchemist.

## Team Execution
- Agent Team Kickoff: [`agent-team-kickoff-2026-02-27.md`](./agent-team-kickoff-2026-02-27.md)
- Day-1 Assignment: [`day1-assignment-2026-02-27.md`](./day1-assignment-2026-02-27.md)
- Daily Status: [`status-2026-02-27.md`](./status-2026-02-27.md)
- **Docker MCP Runbook (MZ-TEAM-002)**: [`docker-mcp-runbook.md`](./docker-mcp-runbook.md)
- Canonical Docker MCP Profile: [`docs/Integration/DOCKER_MCP_CANONICAL_PROFILE.md`](../../../docs/Integration/DOCKER_MCP_CANONICAL_PROFILE.md)
- Operating Playbook: [`docs/architecture/AGENT_TEAM_OPERATING_PLAYBOOK.md`](../../../docs/architecture/AGENT_TEAM_OPERATING_PLAYBOOK.md)
- MZ-TEAM-004 Domain/ML Review: [`mz-team-004-domain-ml-compatibility-review-2026-02-27.md`](./mz-team-004-domain-ml-compatibility-review-2026-02-27.md)
- MZ-TEAM-005 QA Checklist: [`mz-team-005-qa-quality-gate-checklist-2026-02-27.md`](./mz-team-005-qa-quality-gate-checklist-2026-02-27.md)

## Goals
- [x] Refactor service registration in `GaApi/Program.cs` into dedicated extension methods.
- [x] Stabilize namespaces and project structure.
- [x] Implement C# 12+ features (Primary Constructors, Collection Expressions).
- [ ] Progress microservices migration (Theory, etc.).
- [x] Implement YARP Gateway configuration.
- [x] Unified caching strategy (Redis + IMemoryCache using .NET 9 HybridCache).
- [ ] MongoDB query optimization.
- [ ] Enhanced vector models.
- [x] Standardize MCP orchestration via Docker MCP Gateway for all local agents and CLI workflows.

## Tasks
- [x] Create `ServiceCollectionExtensions` for Core, Theory, and AI services.
- [x] Cleanup `GaApi/Program.cs`.
- [x] Audit `GA.MusicTheory.Service` for missing logic from the monolith.
- [x] Configure YARP for routing.
- [x] Modernize service constructors in `GA.Business.ML`.
- [x] Implement robust `ToEmbeddingString()` for all `RagDocumentBase` types.
- [ ] Migrate testing framework from NUnit to xUnit or TUnit for optimal parallel execution and modern testing paradigms.
- [x] Audit and replace fragmented local MCP registrations with `docker mcp` managed servers.
- [x] Document a canonical MCP runtime profile (MongoDB, Redis, Meshy AI, and shared defaults) for IDE + CLI parity.
- [x] Create modernization agent team kickoff with role ownership and sprint backlog.
- [x] Execute MZ-TEAM-001..005 from kickoff backlog with QA-gated completion.

## Future Tracks
- **K8s Deployment** → [`k8s-deployment/index.md`](../k8s-deployment/index.md) — Kubernetes migration strategy (activate when scaling/cloud hosting is needed)
