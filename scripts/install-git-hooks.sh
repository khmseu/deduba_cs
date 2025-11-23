#!/usr/bin/env bash
# Install local git hooks by setting git's core.hooksPath to .githooks
# Use: ./scripts/install-git-hooks.sh

set -euo pipefail

REPO_ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)
HOOK_DIR="$REPO_ROOT/.githooks"

if [ ! -d "$HOOK_DIR" ]; then
  echo "Creating hooks directory: $HOOK_DIR"
  mkdir -p "$HOOK_DIR"
fi

echo "Ensuring hooks are executable"
chmod +x "$HOOK_DIR"/* || true

echo "Setting git core.hooksPath to $HOOK_DIR"
git config core.hooksPath "$HOOK_DIR"

echo "Installed hooks. To undo, run: git config --unset core.hooksPath"
