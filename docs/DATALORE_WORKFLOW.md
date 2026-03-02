# JetBrains Datalore in GuitarAlchemist Development

## Summary

**Yes — Datalore can help our development, but indirectly.** It is a **web-based notebook platform** that integrates well with the JetBrains ecosystem and Git workflows, rather than being “embedded” inside Rider/IntelliJ like a local notebook plugin.

Think of it as a **research satellite**:

- **Datalore**: explore / prototype / visualize / evaluate
- **JetBrains IDEs (Rider/IntelliJ/PyCharm)**: productize / refactor / test / ship

## What integrates well

### Git (GitHub/GitLab/etc.)
Recommended flow:

1. Edit core logic in your IDE
2. Push changes
3. Pull/sync into Datalore for exploration + visualization
4. Commit notebooks or extract modules back into the repo

### JetBrains accounts & permissions
- Same JetBrains identity + org permissions
- Easy sharing of notebooks/reports

### Language/tool alignment
- **Python** prototyping aligns with PyCharm (or Rider + Python sidecar workflows)
- **Kotlin/Scala** aligns with IntelliJ
- **SQL** aligns with DataGrip

### Copy/paste parity
Code written in Datalore is idiomatic and typically pastes cleanly into projects (no “special notebook DSL”).

## What does *not* exist (yet)

- No “Open in Rider” live notebook plugin
- No local kernel execution *inside* Rider/IntelliJ
- No IDE debugger attachment into a running Datalore kernel

So: **cloud notebook ↔ IDE via Git**, not “notebook inside the IDE”.

## Where Datalore is high leverage for GuitarAlchemist

### Feature/embedding R&D
- Try new spectral features, embeddings, clustering, retrieval metrics
- Run ablations and generate plots/tables before committing to production code

### Dataset inspection + labeling utilities
- Identify drift, outliers, bad labels, class imbalance
- Create repeatable “sanity check” notebooks

### Evaluation harness notebooks
- Compare candidate scoring functions (e.g., voice-leading cost vs. ranking quality)
- Produce shareable charts and summary tables

### Reproducible research artifacts
- Keep “executable explanation” notebooks for tricky math (DFT/DWT, similarity geometry, etc.)

## What we should *not* use Datalore for

- Core product logic (we want deterministic, testable code)
- Anything requiring tight step-debugging against the running product
- Long-term pipelines that belong in CI/CD (unless we explicitly want scheduled research reports)

## A workflow that works well (our “R&D → crystallize” pattern)

### Recommended pattern

1. **Prototype** in notebooks (Datalore / local polyglot) until the idea proves value
2. **Extract** the logic into the appropriate library/service
3. **Add tests** so the result is deterministic and maintainable
4. Keep the notebook as:
   - the benchmark
   - the explainer
   - the visualization/reporting artifact

### Repo placement (pragmatic)
- Keep notebooks in `Notebooks/` (current convention) or a clearly-non-prod folder such as `Experiments/`.
- When the work becomes product code:
  - Put AI/ML functionality into the repo’s AI/ML layer (per architecture rules).
  - Keep domain primitives in the Core/Domain projects (pure, deterministic).

> Guideline: notebooks should *call* exported modules rather than re-implement the full algorithm inline.

## Datalore vs local notebooks (quick decision table)

| Need | Datalore | Local Jupyter | VS Code notebooks |
|---|---|---|---|
| Shareable research artifacts | Strong | Medium | Medium |
| Collaboration / team visibility | Strong | Weak | Medium |
| Tight dev loop with product code | Weak | Strong | Strong |
| Debugger integration | Weak | Medium | Strong |
| Reporting (tables/plots/dashboards) | Strong | Medium | Medium |
| Offline / no-cloud constraint | Weak | Strong | Strong |

## Practical checklist

- Decide whether datasets are allowed to leave the machine/network (policy/security).
- Prefer **Git-synced notebooks** rather than one-off copies.
- When a notebook proves value:
  - extract to library code
  - add tests
  - keep notebook as evidence + benchmark

