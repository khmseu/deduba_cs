#!/usr/bin/env bash
# Usage:
#   OWNER=khmseu REPO=deduba_cs DRY_RUN=true ./purge-actions-storage-gh.sh
# Defaults:
#   DRY_RUN=true (set to "false" to actually delete)
# Requires: gh, jq
set -euo pipefail

OWNER="${OWNER:-}"
REPO="${REPO:-}"
DRY_RUN="${DRY_RUN:-true}"
SLEEP_BETWEEN="${SLEEP_BETWEEN:-0.2}"

if [ -z "$OWNER" ] || [ -z "$REPO" ]; then
  echo "Error: set OWNER and REPO environment variables."
  echo "Example: OWNER=khmseu REPO=deduba_cs DRY_RUN=true ./purge-actions-storage-gh.sh"
  exit 2
fi

command -v gh >/dev/null 2>&1 || { echo "gh CLI not found. Install from https://github.com/cli/cli"; exit 1; }
command -v jq >/dev/null 2>&1 || { echo "jq not found. Install jq to parse JSON."; exit 1; }

# Check gh auth status (non-fatal)
if ! gh auth status >/dev/null 2>&1; then
  echo "gh not authenticated. Run 'gh auth login' or set GH_TOKEN env var to a PAT, then try again."
  exit 1
fi

echo "DRY_RUN=$DRY_RUN; OWNER=$OWNER REPO=$REPO"
echo "Listing & (optionally) deleting repository artifacts, caches, and workflow runs."
echo

# Artifacts
echo "=== Artifacts ==="
# Use --paginate to get all pages
gh api --paginate "repos/$OWNER/$REPO/actions/artifacts" -q '.artifacts[] | {id,name,size_in_bytes,created_at,expires_at}' | jq -c '.' | while read -r item; do
  id=$(echo "$item" | jq -r '.id')
  name=$(echo "$item" | jq -r '.name')
  size=$(echo "$item" | jq -r '.size_in_bytes')
  created=$(echo "$item" | jq -r '.created_at')
  expires=$(echo "$item" | jq -r '.expires_at // "null"')
  if [ "$DRY_RUN" = "true" ]; then
    echo "[DRY] artifact id=$id name=\"$name\" size=$size created=$created expires=$expires"
  else
    echo "Deleting artifact id=$id name=\"$name\""
    gh api -X DELETE "repos/$OWNER/$REPO/actions/artifacts/$id"
    sleep "$SLEEP_BETWEEN"
  fi
done

# Caches
echo
echo "=== Caches ==="
# actions_caches array
gh api --paginate "repos/$OWNER/$REPO/actions/caches" -q '.actions_caches[] | {id,size_in_bytes,last_accessed_at,created_at}' | jq -c '.?' | while read -r item; do
  [ -z "$item" ] && continue
  id=$(echo "$item" | jq -r '.id')
  size=$(echo "$item" | jq -r '.size_in_bytes')
  last=$(echo "$item" | jq -r '.last_accessed_at // .created_at')
  if [ "$DRY_RUN" = "true" ]; then
    echo "[DRY] cache id=$id size=$size last_accessed=$last"
  else
    echo "Deleting cache id=$id"
    gh api -X DELETE "repos/$OWNER/$REPO/actions/caches/$id"
    sleep "$SLEEP_BETWEEN"
  fi
done

# Workflow runs
echo
echo "=== Workflow runs ==="
echo "By default the script will not delete workflow runs. To delete runs, set DELETE_RUNS=true."
DELETE_RUNS="${DELETE_RUNS:-false}"
if [ "$DELETE_RUNS" != "true" ]; then
  echo "Skipping run deletion (set DELETE_RUNS=true to delete workflow runs)."
else
  gh api --paginate "repos/$OWNER/$REPO/actions/runs" -q '.workflow_runs[] | {id,name,created_at,event,conclusion}' | jq -c '.?' | while read -r item; do
    [ -z "$item" ] && continue
    id=$(echo "$item" | jq -r '.id')
    name=$(echo "$item" | jq -r '.name')
    created=$(echo "$item" | jq -r '.created_at')
    if [ "$DRY_RUN" = "true" ]; then
      echo "[DRY] workflow run id=$id name=\"$name\" created=$created"
    else
      echo "Deleting workflow run id=$id name=\"$name\""
      gh api -X DELETE "repos/$OWNER/$REPO/actions/runs/$id"
      sleep "$SLEEP_BETWEEN"
    fi
  done
fi

echo
echo "Finished. DRY_RUN=$DRY_RUN. If DRY_RUN=true, re-run with DRY_RUN=false to perform deletion (and set DELETE_RUNS=true to delete runs)."