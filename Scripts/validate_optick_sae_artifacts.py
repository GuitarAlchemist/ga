"""
Validate every committed OPTIC-K SAE artifact against the JSON Schema.

WHY: PR #82 shipped a synthetic-data smoke artifact with three metadata bugs that
the existing test suite didn't catch (partitions_used missing ROOT, narrative
inconsistency, etc.). This script is the regression guard so future drift is
caught at CI time, not by spotting it in code review.

USAGE:
    python Scripts/validate_optick_sae_artifacts.py
    # Exit 0: all artifacts validate
    # Exit 1: at least one artifact failed schema validation
    # Exit 2: schema or fixture missing
"""
from __future__ import annotations

import json
import sys
from pathlib import Path


def main() -> int:
    repo = Path(__file__).resolve().parent.parent
    schema_path = repo / "docs" / "contracts" / "optick-sae-artifact.schema.json"
    artifacts_root = repo / "state" / "quality" / "optick-sae"

    if not schema_path.exists():
        print(f"FAIL: missing schema {schema_path}", file=sys.stderr)
        return 2
    if not artifacts_root.exists():
        print(f"INFO: {artifacts_root} does not exist; nothing to validate.")
        return 0

    try:
        import jsonschema
    except ImportError:
        print("FAIL: jsonschema not installed. Install with `pip install jsonschema`.",
              file=sys.stderr)
        return 2

    schema = json.loads(schema_path.read_text(encoding="utf-8"))
    artifact_paths = sorted(artifacts_root.rglob("optick-sae-artifact.json"))
    if not artifact_paths:
        print(f"INFO: no artifacts under {artifacts_root}; nothing to validate.")
        return 0

    print(f"Validating {len(artifact_paths)} artifact(s) against {schema_path.name}")

    failures = []
    for path in artifact_paths:
        rel = path.relative_to(repo)
        try:
            artifact = json.loads(path.read_text(encoding="utf-8"))
            jsonschema.validate(artifact, schema)
            print(f"  PASS  {rel}")
        except json.JSONDecodeError as e:
            print(f"  FAIL  {rel} (invalid JSON: {e.msg})")
            failures.append((rel, f"invalid JSON: {e.msg}", []))
        except jsonschema.ValidationError as e:
            loc = " > ".join(str(p) for p in e.absolute_path) or "<root>"
            print(f"  FAIL  {rel}")
            print(f"        at {loc}: {e.message}")
            failures.append((rel, e.message, list(str(p) for p in e.absolute_path)))

    if failures:
        print(f"\n{len(failures)} artifact(s) failed validation:", file=sys.stderr)
        for rel, msg, location in failures:
            loc = " > ".join(location) or "<root>"
            print(f"  {rel}: {msg} (at {loc})", file=sys.stderr)
        return 1

    print(f"\nAll {len(artifact_paths)} artifact(s) validated successfully.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
