#!/usr/bin/env bash
set -euo pipefail
# Simple script to list and optionally delete GitHub Actions artifacts older than DAYS
# Usage: cleanup-artifacts-gh.sh [DAYS] [OWNER/REPO]

DAYS=${1:-30}
REPO=${2:-$(git remote get-url origin 2>/dev/null || true)}

if ! command -v gh >/dev/null 2>&1; then
	echo 'gh CLI is required. Install CLI from https://cli.github.com/ or use package manager.' >&2
	exit 1
fi
if ! command -v jq >/dev/null 2>&1; then
	echo 'jq is required for JSON processing. Install jq via apt/yum/brew.' >&2
	exit 1
fi

if [[ -z ${REPO} ]]; then
	echo 'Unable to determine repository. Pass owner/repo as second argument (e.g. khmseu/deduba_cs)' >&2
	exit 1
fi

if [[ -n ${REPO} ]]; then
	if [[ ${REPO} =~ github.com[:/]([^/]+)/([^/]+)(\.git)?$ ]]; then
		owner="${BASH_REMATCH[1]}"
		repo_name="${BASH_REMATCH[2]}"
		REPO="${owner}/${repo_name}"
	fi
fi

echo "Repo: ${REPO}"
echo "Checking artifacts older than ${DAYS} days..."

thresh_epoch=$(($(date +%s) - DAYS * 24 * 3600))

artifacts_json=$(gh api -X GET "/repos/${REPO}/actions/artifacts?per_page=100" --jq '.artifacts')
if [[ -z ${artifacts_json} ]]; then
	echo "No artifacts found in ${REPO}."
	exit 0
fi

to_delete=$(echo "${artifacts_json}" | jq -r --arg THRESH "${thresh_epoch}" '.[] | select((.created_at | fromdate) < ($THRESH | tonumber)) | {id,name,created_at,size_in_bytes} | @base64')

if [[ -z ${to_delete} ]]; then
	echo "No artifacts older than ${DAYS} days found."
	exit 0
fi

echo "Artifact candidates to delete (older than ${DAYS} days):"
echo "${to_delete}" | while read -r art; do
	id=$(echo "${art}" | base64 --decode | jq -r '.id')
	name=$(echo "${art}" | base64 --decode | jq -r '.name')
	created=$(echo "${art}" | base64 --decode | jq -r '.created_at')
	size=$(echo "${art}" | base64 --decode | jq -r '.size_in_bytes')
	echo "  - id: ${id} | name: ${name} | created: ${created} | size: ${size}"
done

echo
read -r -p "Proceed to delete these artifacts? Type YES to confirm: " CONF
if [[ ${CONF} != "YES" ]]; then
	echo "Aborting per user request."
	exit 0
fi

echo "Deleting artifacts..."
deleted=0
echo "${to_delete}" | while read -r art; do
	id=$(echo "${art}" | base64 --decode | jq -r '.id')
	name=$(echo "${art}" | base64 --decode | jq -r '.name')
	echo "Deleting artifact ${id} (${name})"
	gh api -X DELETE "/repos/${REPO}/actions/artifacts/${id}"
	deleted=$((deleted + 1))
done

echo "Deleted ${deleted} artifacts older than ${DAYS} days (if any)."
