---
status: accepted
date: 2026-11-17
---

# GA stays a modular monolith; no GitHub Packages registry yet

Two proposals were weighed on 2026-11-17 and both were declined for now:
splitting GA into microservices, and publishing GA through GitHub Packages
(NuGet libraries or `ghcr.io` images). GA is one deployable (GaApi, see
[ADR-0005](0005-gaapi-single-canonical-chat-host.md)) built from the five-layer
model in `docs/architecture/layers.md`. Decoupling happens through in-process
seams (for example `IChatIntake`), not through the network or a package feed.

## Why no microservices

- **Splitting hosts already cost us.** GaApi and GaChatbot.Api drifted on cascade
  provider, readiness probes and routing settings. ADR-0005 merged them, and the
  2026-11 cloudflared cutover finishes that work. Splitting again would bring the
  drift back.
- **The open problem is latency.** On 2026-11-17 the agent path took 27–33 s to
  the first streamed token. Each service hop adds network time, serialization and
  partial-failure modes.
- **Nothing calls for it.** GaApi runs as one instance on one machine, with one
  owner and no component that needs to scale on its own.
- **Separate processes already exist where they are justified**, because the
  runtime or the resources differ: Ollama (GPU), ix (Rust), cloudflared.

**Revisit when** a component needs another machine or runtime (for example the
OPTIC-K index outgrowing GaApi's memory), crashes or ships on a different cadence,
or gets its own owner. Extract that one component, behind the seam it already has.

## Why no GitHub Packages registry

- **No .NET consumer outside this repo.** ix, tars and Demerzel exchange data
  with GA through JSON-on-disk contracts (`docs/contracts/`), not by referencing
  GA assemblies, so a NuGet feed would have no reader.
- **Deployment is already versioned.** GaApi deploys from source to
  `.deploy/releases/<sha>`, behind a `current` junction that gives a one-step
  rollback. A container image adds nothing while GaApi runs on this Windows host.
- **Large artifacts already have a home.** The OPTIC-K index ships as a GitHub
  release asset.

**Revisit when** a sibling repo needs GA's .NET code (then publish NuGet to
GitHub Packages), or GaApi moves off this machine (then publish an image to
`ghcr.io`, tagged by commit SHA).

## Related

Chat transports are being decoupled from chat runs in two stages. Stage 1 makes
`IChatIntake` return a typed in-process event stream. Stage 2, a broker-backed
implementation of the same seam, is deferred until a second instance, stream
resume or background runs are actually needed. Stage 2 is the planned route if
the chat path ever does need to leave the process.
