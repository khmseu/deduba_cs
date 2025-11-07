#!/bin/bash
# Simple test script - runs DeDuBa on all workspace files
script -c "'time' -v  timeout -s USR1 1m dotnet run --project=DeDuBa --no-build -- $(echo *)" test.ansi &>/dev/null
