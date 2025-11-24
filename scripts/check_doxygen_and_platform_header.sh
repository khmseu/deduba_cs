#!/usr/bin/env bash
# scripts/check_doxygen_and_platform_header.sh
# Verifies: 1) Doxygen emits zero warnings; 2) Platform.h is the first include in native shim .cpp files
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

DOXYFILE="docs/Doxyfile"
if [ ! -f "$DOXYFILE" ]; then
  echo "Doxygen configuration not found at $DOXYFILE"
  exit 1
fi

# Ensure doxygen exists
if ! command -v doxygen >/dev/null 2>&1; then
  echo "Doxygen is required but not found. Please install it (apt-get install doxygen)"
  exit 1
fi

TMP_LOG=$(mktemp -t doxygen-log.XXXXXX)
trap 'rm -f "$TMP_LOG"' EXIT

echo "Running doxygen ($DOXYFILE) — output captured in $TMP_LOG"
pushd docs >/dev/null
if ! doxygen Doxyfile 2>&1 | tee "$TMP_LOG"; then
  echo "Doxygen returned a non-zero status — failing CI. See $TMP_LOG for details"
  exit 1
fi
popd >/dev/null

# Look for warnings
WARNINGS=$(grep -i "warning:" "$TMP_LOG" || true)
if [ -n "$WARNINGS" ]; then
  echo "Doxygen reported warnings — failing CI. Warnings:" >&2
  echo "----------------------------------------" >&2
  echo "$WARNINGS" >&2
  echo "----------------------------------------" >&2
  exit 1
fi

# Now verify Platform.h is the first #include in each native shim .cpp file
FAILED_FILES=()

# Limit check to shim src directories where this is required
SHIM_DIRS=(OsCallsCommonShim OsCallsLinuxShim OsCallsWindowsShim)
for dir in "${SHIM_DIRS[@]}"; do
  if [ -d "$dir/src" ]; then
    while IFS= read -r -d '' file; do
      # Use awk to determine the first non-comment, non-empty #include line
      first_include=$(awk '
        BEGIN { in_comment = 0 }
        {
          line = $0;
          # remove CR
          sub("\r$", "", line);
          if (in_comment) {
            # check for end of block comment
            if (index(line, "*/") > 0) {
              in_comment = 0;
              # remove up to end of comment and continue processing the rest of the line
              sub("^.*\*\/", "", line);
              if (match(line, "^[[:space:]]*#\s*include")) { print line; exit; }
            }
            next;
          }
          # start of block comment
          if (match(line, "^[[:space:]]*/\*")) {
            if (index(line, "*/") > 0) {
              sub("^.*\*\/", "", line);
            } else {
              in_comment = 1; next;
            }
          }
          # single line comment
          if (match(line, "^[[:space:]]*//")) next;
          # blank
          if (match(line, "^[[:space:]]*$")) next;
          if (match(line, "^[[:space:]]*#\s*include")) { print line; exit; }
        }
      ' "$file" || true)

      # Normalize whitespace and check content
      if [ -z "$first_include" ]; then
        # No includes at all — optional; skip
        continue
      fi
      # Extract header file
      # match both #include "Platform.h" and #include <Platform.h>
      header=$(echo "$first_include" | sed -E 's/^\s*#\s*include\s*[<\"]([^>\"]+)[>\"].*/\1/')
      if [ "$header" != "Platform.h" ]; then
        FAILED_FILES+=("$file (first include: ${header:-<none>})")
      fi
    done < <(find "$dir/src" -type f -name '*.cpp' -print0)
  fi
done

if [ ${#FAILED_FILES[@]} -ne 0 ]; then
  echo "Found files where the first #include is not Platform.h:" >&2
  for f in "${FAILED_FILES[@]}"; do
    echo " - $f" >&2
  done
  echo "Please ensure that the first include in native shim .cpp files is \"Platform.h\"." >&2
  exit 1
fi

# Success
echo "Doxygen produced no warnings and Platform.h is the first include for all shim .cpp files." 
exit 0
