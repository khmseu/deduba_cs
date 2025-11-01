#!/usr/bin/env bash
set -Eeuo pipefail

# Determine project root and site dir (mirror serve-docs.sh)
ROOT_DIR="${workspaceFolder:-$(cd "$(dirname "$0")/.." && pwd)}"
SITE_DIR="${ROOT_DIR}/docs/_site"

# Find a running python http.server whose CWD is SITE_DIR
PIDS=$(pgrep -f "http\.server" || true)
FOUND_PID=""
for pid in ${PIDS}; do
	# Verify working directory matches SITE_DIR
	if [[ -L "/proc/${pid}/cwd" ]]; then
		CWD=$(readlink -f "/proc/${pid}/cwd" || true)
		if [[ ${CWD} == "${SITE_DIR}" ]]; then
			FOUND_PID="${pid}"
			break
		fi
	fi
done

if [[ -z ${FOUND_PID} ]]; then
	echo "Error: No docs server appears to be running."
	echo "Start it first with: Tasks: Run Task -> Docs: Serve site (local)"
	exit 1
fi

# Extract the port from the process command line (last arg passed to http.server)
ARGS=$(ps -p "${FOUND_PID}" -o args=)
PORT=$(awk 'NF{print $NF}' <<<"${ARGS}")

# Validate PORT is a number; fallback to 8000 if not
if ! [[ ${PORT} =~ ^[0-9]+$ ]]; then
	PORT=8000
fi

URL="http://localhost:${PORT}"
echo "Opening ${URL}"

# Open in browser
if command -v xdg-open >/dev/null 2>&1; then
	xdg-open "${URL}"
elif command -v open >/dev/null 2>&1; then
	open "${URL}"
else
	echo "Could not find xdg-open or open. Please open manually: ${URL}"
fi
