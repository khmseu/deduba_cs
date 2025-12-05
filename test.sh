#!/bin/bash
# Simple test script - runs DeDuBa on all workspace files

# Derive project directory from script location
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Set library path for native interop
export LD_LIBRARY_PATH="${SCRIPT_DIR}/OsCallsCommonShim/bin/Debug/net8.0:${SCRIPT_DIR}/OsCallsLinuxShim/bin/Debug/net8.0:${LD_LIBRARY_PATH}"

cd "${SCRIPT_DIR}" || exit 1

# Default testing archive path to a workspace-local ARCHIVE5 (overridable via DEDU_ARCHIVE_ROOT)
export DEDU_ARCHIVE_ROOT="${SCRIPT_DIR}/ARCHIVE5"
rm -rf "${DEDU_ARCHIVE_ROOT}" || true
script -c "'time' -v  timeout -s USR1 1m dotnet run --project=DeDuBa --no-build -- $(echo *)" ../test.ansi &>/dev/null
