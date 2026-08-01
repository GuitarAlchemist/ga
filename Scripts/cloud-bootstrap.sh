#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

required_commands=(dotnet node npm cargo rustc pwsh gh)
missing=()

for command_name in "${required_commands[@]}"; do
  if ! command -v "$command_name" >/dev/null 2>&1; then
    missing+=("$command_name")
  fi
done

if ((${#missing[@]} > 0)); then
  printf 'Missing cloud-development commands: %s\n' "${missing[*]}" >&2
  exit 1
fi

printf 'Guitar Alchemist cloud toolchain\n'
printf '  dotnet: %s\n' "$(dotnet --version)"
printf '  node:   %s\n' "$(node --version)"
printf '  npm:    %s\n' "$(npm --version)"
printf '  rustc:  %s\n' "$(rustc --version)"
printf '  cargo:  %s\n' "$(cargo --version)"
printf '  pwsh:   %s\n' "$(pwsh -NoLogo -NoProfile -Command '$PSVersionTable.PSVersion.ToString()')"
printf '  gh:     %s\n' "$(gh --version | head -n 1)"

# Keep CI and local Dev Containers fast. Codespaces is the only environment
# where the CLI is installed automatically; elsewhere the VS Code extension or
# an explicit operator install remains authoritative.
if [[ "${CODESPACES:-false}" == "true" ]] && ! command -v claude >/dev/null 2>&1; then
  npm install --global @anthropic-ai/claude-code
fi

cat <<'EOF'

Cloud environment is ready.
Run the bounded validation used by GitHub Actions with:
  bash Scripts/cloud-validate.sh
EOF
