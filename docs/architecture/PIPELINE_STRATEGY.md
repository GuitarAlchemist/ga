# Pipeline & Benchmarking Strategy

## Executive Summary
**Goal:** Create a robust, autonomous pipeline for Guitar Alchemist without "tool bloat."
**Core Philosophy:** "Logic in C# (GaCLI), Orchestration in n8n, Verification in CI."

We will prioritize **Self-Hosted n8n** for orchestration and **GaCLI** as the universal runner. We will **NOT** create a separate .NET project for benchmarking at this time; instead, we will treat `GaCLI` as the unified entry point.

---

## 1. Tool Selection

We selected tools based on:
1.  **Affinity with Current Stack:** C#/.NET Core, Docker.
2.  **Local/Offline Capability:** "Antigravity" mindset.
3.  **Simplicity:** Avoiding Python heavy frameworks (Airflow) unless necessary.

| Function | Tool | Status | Rejection Reason for Alts |
| :--- | :--- | :--- | :--- |
| **Orchestration** | **n8n** (Self-Hosted Docker) | **Selected** (Phase 21) | **Node-RED:** Less "LLM-native", overlapping feature set.<br>**Airflow:** Python-centric, high overhead for valid C# stack. |
| **Logic/Runner** | **GaCLI** (Dotnet Tool) | **Selected** | **Separate Project:** Premature complexity. DI container reuse is critical. |
| **Local CI** | **nektos/act** (Local GHA) | **Recommended** | **Jenkins/TeamCity:** Too heavy for local "inner loop" dev. |
| **Scheduled Jobs** | **n8n Cron Nodes** | **Selected** | **OS Cron:** Less visibility/logging than n8n dashboard. |

---

## 2. Decision: Separate .NET Benchmark Project?
**Verdict: NO.**

### Rationale
1.  **Code Reuse:** Benchmarks (e.g., retrieving embeddings, generating tabs, analysing results) rely 100% on `GA.Business.Core` and `GA.Business.ML`. `GaCLI` already wires up the complex Dependency Injection (DI) graph needed for these services.
2.  **Single Artifact:** Deployment is simpler if `GaCLI` is the "Swiss Army Knife".
3.  **Docker Friendliness:** One Docker image (`ga-cli`) can be reused by n8n for Ingestion, Benchmarking, and Maintenance just by changing the command arguments.

### Implementation Strategy
Instead of a new project, we will organize `GaCLI` commands into specific "Suites":
*   `dotnet run -- benchmark-quality` (Existing)
*   `dotnet run -- benchmark-retrieval` (Vector validation)
*   `dotnet run -- benchmark-groundedness` (LLM/Chatbot QA)

*Refactor triggers:* We will only split into `GA.Benchmarks` if the benchmark dependencies (e.g., heavy plotting libraries, python bridges) start polluting the main CLI's startup time or deployment size.

---

## 3. The "Antigravity" Pipeline Architecture

This pipeline runs locally, leveraging Docker.

### A. The "Inner Loop" (Code & Verify)
*   **Trigger:** User saves file / runs test.
*   **Tool:** `dotnet test` or `act` (Local GitHub Actions).
*   **Scope:** Unit Tests, Fast Integration Tests.

### B. The "Orchestration Loop" (n8n + GaCLI)
*   **Trigger:** Webhook (Tab Pust), Schedule (Nightly), or Manual "Conductor" command.
*   **Environment:** Docker Compose Network (`ga-network`).

**Workflow: "Nightly Groundedness Check"**
1.  **n8n Schedule Node:** Triggers every night at 3 AM.
2.  **n8n Docker Node:** Runs `ga-cli` container.
    *   Command: `benchmark-groundedness --limit 50 --output /data/reports/latest.json`
3.  **n8n File Read Node:** Reads `latest.json`.
4.  **n8n JavaScript Function:** Analytics (Pass/Fail ratio, Regression check).
5.  **n8n Email/Slack Node:** Alerts user if groundedness drops < 85%.

### C. The "Ad-Hoc" Loop (Agentic)
*   User asks "Run the benchmarks".
*   Agent calls `run_command("dotnet run -- benchmark-groundedness")`.
*   Agent reads output.

---

## 4. Roadmap Integration

### Phase 21: n8n Orchestration (Refined)
*   [ ] **21.1 Infrastructure:**
    *   Hardening `docker-compose` for n8n + Qdrant + MongoDB networking.
    *   Ensure `GaCLI` references correct hostnames (container-to-container) via env vars.
*   [ ] **21.2 Workflows:**
    *   **Tab Ingestion:** Webhook -> CLI `ingest-corpus` -> Vector Index.
    *   **Groundedness:** Schedule -> CLI `benchmark-groundedness`.
*   [ ] **21.3 "act" Integration:** Add `act` to devbox for local CI reproduction.
