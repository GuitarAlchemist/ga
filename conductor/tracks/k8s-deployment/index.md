# Track: Kubernetes Deployment

## Overview
Status: **Future / Not Started**
Owner: TBD
Priority: Low — revisit when scaling or cloud hosting requires it

## Decision Context

**Current deployment strategy:** Docker Compose (see `docker-compose.yml`)
**Current dev orchestration:** .NET Aspire (`AllProjects.AppHost`)

Docker Compose is the right tool today. Kubernetes becomes relevant when one or more of the
following is true:

- Targeting a managed cloud cluster (AKS, EKS, GKE)
- Need horizontal scaling / autoscaling for any service
- Team grows to 5+ developers requiring shared environments
- Zero-downtime blue/green or canary deployments are needed

---

## Pre-conditions Before Starting This Track

These must be true before any K8s work begins:

- [ ] All services build cleanly from Docker Compose (`docker compose up --build`)
- [ ] `GA.MusicTheory.Service` is containerized and added to `docker-compose.yml`
- [ ] `GA.MusicTheory.Service` is reachable via YARP gateway in Compose
- [ ] Secrets managed via `.env` / secret store (not hardcoded defaults)
- [ ] Health check endpoints are stable on all custom services
- [ ] Ollama GPU workload strategy is decided (CPU-only in K8s, or sideloaded)
- [ ] Vector DB (Qdrant) volume backup/restore story defined

---

## Architecture Intent

### Namespace
All GA workloads run in a single namespace: `guitar-alchemist`

### Deployment Groups

#### Tier 1 — Stateless Application Services (Deployments)
These are safe to scale horizontally:

| Service | Image Source | Replicas | Notes |
|---|---|---|---|
| `gaapi` | `Apps/GaApi/Dockerfile` | 2+ | YARP gateway; stateless |
| `ga-music-theory-svc` | `Apps/ga-server/GA.MusicTheory.Service/Dockerfile` | 2+ | Core domain service |
| `chatbot` | `Apps/GuitarAlchemistChatbot/Dockerfile` | 1–2 | Blazor Hybrid |
| `ga-client` | `Apps/ga-client/Dockerfile` | 2+ | React static, nginx |
| `ga-dashboard` | `Apps/ga-dashboard/Dockerfile` | 1–2 | Angular static, nginx |
| `graphiti-service` | `Apps/ga-graphiti-service/Dockerfile` | 1 | State-dependent on FalkorDB |

#### Tier 2 — Stateful Databases (StatefulSets)
Do NOT use Deployments for these:

| Service | Image | Storage | Notes |
|---|---|---|---|
| `mongodb` | `mongo:7.0.5-alpine` | PVC 20Gi | Primary document store |
| `falkordb` | `falkordb/falkordb:4.0.0` | PVC 10Gi | Graph DB for Graphiti |
| `qdrant` | `qdrant/qdrant:v1.10.1` | PVC 10Gi | Vector embeddings |

#### Tier 3 — Heavy AI Workloads (Special Handling)
These require careful node/resource scheduling:

| Service | Image | Notes |
|---|---|---|
| `ollama` | `ollama/ollama:0.1.32` | GPU node required OR use cloud-hosted LLM endpoint instead |

> **Recommendation:** Replace self-hosted Ollama with a managed API (Azure OpenAI, etc.)
> before K8s migration. GPU node pools are expensive and complex.

#### Tier 4 — Observability (Deployments)
| Service | Image | Notes |
|---|---|---|
| `jaeger` | `jaegertracing/all-in-one:1.51.0` | Single replica; for tracing |
| `mongo-express` | `mongo-express:1.0.2-alpine` | Dev/staging only, not prod |
| `redis-commander` | TBD | Dev/staging only |

---

## File Structure (When Implemented)

```
k8s/
├── namespace.yaml
├── configmaps/
│   ├── gaapi-config.yaml
│   ├── graphiti-config.yaml
│   └── ollama-config.yaml
├── secrets/
│   └── .gitkeep          # Secrets via external vault, NOT stored here
├── deployments/
│   ├── gaapi.yaml
│   ├── ga-music-theory-svc.yaml
│   ├── chatbot.yaml
│   ├── ga-client.yaml
│   ├── ga-dashboard.yaml
│   ├── graphiti-service.yaml
│   └── jaeger.yaml
├── statefulsets/
│   ├── mongodb.yaml
│   ├── falkordb.yaml
│   └── qdrant.yaml
├── services/
│   ├── gaapi-svc.yaml
│   ├── ga-music-theory-svc.yaml
│   ├── mongodb-svc.yaml
│   ├── falkordb-svc.yaml
│   ├── qdrant-svc.yaml
│   └── graphiti-svc.yaml
├── ingress/
│   └── gaapi-ingress.yaml   # Single ingress → YARP → downstream
└── hpa/
    ├── gaapi-hpa.yaml
    └── ga-music-theory-hpa.yaml
```

---

## Key Design Decisions (Pre-decided)

### Ingress Strategy
Single external entry point → `gaapi` (YARP gateway) → all other services as ClusterIP only.
This mirrors the current Compose architecture with no changes to service-to-service routing.

### Secrets
Use Kubernetes Secrets sourced from an external vault:
- Azure Key Vault (if deploying to AKS)
- AWS Secrets Manager (if deploying to EKS)
- HashiCorp Vault (if self-hosted)

MongoDB passwords, API keys, and connection strings must **never** be in plaintext YAML.

### Health Checks
All custom services already have `/health` endpoints. Map directly to K8s:
- `livenessProbe` → `/health` (is the process alive?)
- `readinessProbe` → `/health/ready` (is it ready to serve traffic?)

### Resource Limits (Initial Starting Point)
Carry over from `docker-compose.yml` resource constraints:

| Service | Memory Request | Memory Limit | CPU Request | CPU Limit |
|---|---|---|---|---|
| gaapi | 512Mi | 2Gi | 500m | 2000m |
| ga-music-theory-svc | 256Mi | 1Gi | 250m | 1000m |
| graphiti-service | 512Mi | 1.5Gi | 500m | 1500m |
| mongodb | 512Mi | 1Gi | 500m | 1000m |
| qdrant | 256Mi | 1Gi | 250m | 1000m |
| ollama | 2Gi | 4Gi | 1000m | 2000m |

---

## Tasks (For When This Track Activates)

- [ ] Validate `docker compose up --build` succeeds for all services
- [ ] Add `GA.MusicTheory.Service` to `docker-compose.yml`
- [ ] Create `k8s/namespace.yaml`
- [ ] Create ConfigMaps for all environment-based configuration
- [ ] Set up external secret store integration (Vault / Key Vault)
- [ ] Create StatefulSets for MongoDB, FalkorDB, Qdrant with PVCs
- [ ] Create Deployments for all stateless services
- [ ] Create Services (ClusterIP) for all service-to-service comms
- [ ] Create Ingress for `gaapi` with TLS termination
- [ ] Create HPA for `gaapi` and `ga-music-theory-svc`
- [ ] Decide: replace Ollama with managed LLM endpoint?
- [ ] Set up Helm chart for parameterized deployment (dev/staging/prod)
- [ ] CI/CD pipeline to build & push images to container registry
- [ ] Smoke test: deploy to Docker Desktop K8s cluster first
- [ ] Load test with k6 before declaring production-ready

---

## References
- Current compose: [`docker-compose.yml`](../../../docker-compose.yml)
- Modernization track: [`modernization/index.md`](../modernization/index.md)
- Tech stack: [`tech-stack.md`](../../tech-stack.md)
