#!/usr/bin/env bash
# Equivalent to the original inline task command, without improvements.
# Intentionally preserves the exact behavior.

set -e

# shellcheck disable=SC2312
find . -mindepth 2 -maxdepth 2 -name "*.md" -print0 | xargs -0 -I_ bash -c 'echo _; npx -y markdown-table-prettify < _ > _.new && mv -v _.new _ || true'
trunk check -ay

dotnet tool restore >/dev/null 2>&1 || true
(
	dotnet tool run csharpier format . || csharpier format .
)

# shellcheck disable=SC2312
git ls-files -z "*.h" "*.hpp" "*.c" "*.cpp" "*.cc" "*.cxx" "*.hh" | xargs -0 -r clang-format -i
