#!/usr/bin/env bash
set -Eeuo pipefail

debug=0 
if [[ ${debug} -eq 1 ]]; then
	export blind="" # set to ">/dev/null 2>&1" to suppress output
	export nohup="" # set to "nohup" to use nohup
	set -x
	strace -omist -fvs333 -y -yy -t -p $$ &
else
	# export blind=">/dev/null 2>&1"
	# export nohup="nohup"
	export blind=""
	export nohup=""
fi
wait=1s

# Determine project root and site dir
ROOT_DIR="${workspaceFolder:-$(cd "$(dirname "$0")/.." && pwd)}"
SITE_DIR="${SITE_DIR:-${ROOT_DIR}/docs/_site}"
SITE_DIR=$(realpath "${SITE_DIR}")

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
	echo "Error: No docs server appears to be running in ${SITE_DIR}."
	echo "Start it first with: Tasks: Run Task -> Docs: Serve site (local)"
	exit 1
fi

# Extract the port from the process command line (last arg passed to http.server)
ARGS=$(ps -p "${FOUND_PID}" -o args=)
PORT=$(awk 'NF{print $NF}' <<<"${ARGS}")

# Validate port is numeric
if ! [[ ${PORT} =~ ^[0-9]+$ ]]; then
	echo "Error: Could not determine port from process ${FOUND_PID}"
	echo "Process command: ${ARGS}"
	exit 1
fi

URL="http://localhost:${PORT}/"
echo "Opening docs at: ${URL}"

# Ensure graphical environment variables are set
export DISPLAY="${DISPLAY:-:0}"
export DBUS_SESSION_BUS_ADDRESS="${DBUS_SESSION_BUS_ADDRESS:-unix:path=/run/user/$(id -u)/bus}"

# Method 1: xdg-open (Linux standard)
if command -v xdg-open $blind; then
	$nohup xdg-open "${URL}" $blind &
	disown
	echo "Browser launch requested (xdg-open)"
	sleep $wait
	exit 0
fi

# Method 2: macOS open
if command -v open $blind; then
	$nohup open "${URL}" $blind &
	disown
	echo "Browser launch requested (open)"
	sleep $wait
	exit 0
fi

# Method 3: Direct browser invocation
for browser in firefox chromium-browser google-chrome chrome; do
	if command -v "${browser}" $blind; then
		$nohup "${browser}" "${URL}" $blind &
		disown
		echo "Browser launch requested (${browser})"
		sleep $wait
		exit 0
	fi
done

echo "Warning: Could not find a suitable browser. Please open manually: ${URL}"
exit 1
