#!/usr/bin/env bash
# Keep this script LF-only; it is the canonical cross-platform L2 verifier.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

project="Tests/Common/GA.Business.ML.Tests/GA.Business.ML.Tests.csproj"
filter='FullyQualifiedName~KeyIdentificationServiceTests|FullyQualifiedName~ImprovisationSkillTests'

if [[ ! -f "$project" ]]; then
  printf 'Expected test project not found: %s\n' "$project" >&2
  exit 1
fi

printf 'Restoring bounded cloud validation project...\n'
dotnet restore "$project"

printf 'Running North Star correctness smoke tests...\n'
dotnet test "$project" \
  --configuration Release \
  --no-restore \
  --filter "$filter" \
  --logger 'console;verbosity=normal'

printf 'Cloud validation passed.\n'
