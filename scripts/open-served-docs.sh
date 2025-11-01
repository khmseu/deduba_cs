#!/usr/bin/env bash
set -Eeuo pipefail

# Extract the last URL from serve-docs.sh output
# Look for "Serving ... on http://localhost:PORT" in recent terminal output

# Try to find a running Python http.server process serving docs/_site
DOCS_SITE="docs/_site"
PID=$(pgrep -f "python.*http.server.*${DOCS_SITE}" | head -1 || true)

if [[ -z "$PID" ]]; then
  echo "Error: No docs server appears to be running."
  echo "Start it first with: Tasks: Run Task -> Docs: Serve site (local)"
  exit 1
fi

# Extract the port from the process command line
PORT=$(ps -p "$PID" -o args= | grep -oP 'http\.server\s+\K\d+' || true)

if [[ -z "$PORT" ]]; then
  echo "Error: Could not determine port for running docs server (PID: $PID)"
  exit 1
fi

URL="http://localhost:${PORT}"
echo "Opening ${URL}"

# Open in browser
if command -v xdg-open >/dev/null 2>&1; then
  xdg-open "$URL"
elif command -v open >/dev/null 2>&1; then
  open "$URL"
else
  echo "Could not find xdg-open or open. Please open manually: ${URL}"
fi
