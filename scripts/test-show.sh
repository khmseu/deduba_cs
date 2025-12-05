#!/bin/bash
# Simple test script - runs DeDuBa on all workspace files

# Derive workspace root from script location (script is in scripts/ subdirectory)
WORKSPACE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && cd .. && pwd)"

# Set library path for native interop
export LD_LIBRARY_PATH="${WORKSPACE_ROOT}/src/OsCallsCommonShim/bin/Debug/net8.0:${WORKSPACE_ROOT}/src/OsCallsLinuxShim/bin/Debug/net8.0:${LD_LIBRARY_PATH}"

cd "${WORKSPACE_ROOT}" || exit 1

# Default testing archive path to a workspace-local ARCHIVE5 (overridable via DEDU_ARCHIVE_ROOT)
export DEDU_ARCHIVE_ROOT="${WORKSPACE_ROOT}/ARCHIVE5"
rm -rf "${DEDU_ARCHIVE_ROOT}" || true
script -c "'time' -v  timeout -s USR1 1d dotnet run --project=src/DeDuBa --no-build -- $(echo *)" ../test.ansi
