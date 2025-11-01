#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIR="${workspaceFolder:-$(cd "$(dirname "$0")/.." && pwd)}"
SITE_DIR="${ROOT_DIR}/docs/_site"
DOCS_DIR="${ROOT_DIR}/docs"

# Ensure doc site exists
if [[ ! -d "$SITE_DIR" ]] || [[ -z "$(ls -A "$SITE_DIR" 2>/dev/null || true)" ]]; then
  echo "DocFX site missing; building..."
  (cd "$DOCS_DIR" && docfx docfx.json)
fi

# Pick python
PY=python3
command -v python3 >/dev/null 2>&1 || PY=python

# Pick an ephemeral free port
PORT=$($PY -c 'import socket; s=socket.socket(); s.bind(("127.0.0.1",0)); print(s.getsockname()[1]); s.close()')

echo "Serving $SITE_DIR on http://localhost:${PORT}"
cd "$SITE_DIR"
exec "$PY" -m http.server "$PORT"
