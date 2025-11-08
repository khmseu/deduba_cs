#!/bin/bash
# Simple test script - runs DeDuBa on all workspace files
rm -rf ../ARCHIVE4/
script -c "'time' -v  timeout -s USR1 1h dotnet run --project=DeDuBa --no-build -- $(echo *)" ../test.ansi &>/dev/null
