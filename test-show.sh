#!/bin/bash
# Simple test script - runs DeDuBa on all workspace files

# Set library path for native interop
export LD_LIBRARY_PATH=/bigdata/KAI/projects/Backup/deduba_cs/OsCallsShim/bin/Debug/net8.0:${LD_LIBRARY_PATH}

rm -rf ../ARCHIVE4/
script -c "'time' -v  timeout -s USR1 1d dotnet run --project=DeDuBa --no-build -- $(echo *)" ../test.ansi
