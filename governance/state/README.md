# ga Governance State

Belief state persistence directory for Demerzel governance integration.

## Contents

- `beliefs/` — Tetravalent belief states (*.belief.json)
- `pdca/` — PDCA cycle tracking (*.pdca.json)
- `knowledge/` — Knowledge transfer records (*.knowledge.json)
- `snapshots/` — Belief snapshots for reconnaissance (*.snapshot.json)

## File Naming

`{date}-{short-description}.{type}.json`

## Schema Reference

See `governance/demerzel/schemas/` and `governance/demerzel/logic/` for JSON schemas.
