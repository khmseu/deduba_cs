# Artifact cleanup runbook

Purpose: Recover from situations where GitHub Actions artifact storage quota is reached and CI artifact upload steps fail. This runbook describes manual and automated cleanup steps.

## Prerequisites

- [gh CLI](https://cli.github.com/) installed and authenticated (the user must have necessary repo permissions).
- jq installed on the local machine for JSON processing.
- Admin / write permissions for the repo to delete artifacts.

## Quick recovery (manual steps)

1. Check repository artifact usage:
   - GitHub UI: Repository → Settings → Actions → Usage → Artifacts
   - Confirm whether usage is near or above quota.

2. Inspect artifacts in the repo and delete older/unneeded ones via the UI.
   - Use the 'Actions' → 'Artifacts' page and click the 'Delete' button.

3. Re-run the failed workflow manually from the GitHub Actions UI for the specific run or re-push/re-tag to re-trigger the workflow.

## Automated cleanup (safe approach via gh CLI)

1. List artifacts and review them before deletion using the included script:

```bash
# Make script executable
chmod +x scripts/cleanup-artifacts-gh.sh
# Dry run: change DAYS=1 (optional) to quickly find very old artifacts, otherwise use 30
scripts/cleanup-artifacts-gh.sh 30 khmseu/deduba_cs
```

- The script will prompt for `YES` before deleting – do NOT run as an unattended job without manual review unless you deliberately change it.

## GitHub Actions automated cleanup job (CI)

- A `artifact-cleanup` job has been added to `.github/workflows/ci.yml`. It runs on `schedule` (daily) and `workflow_dispatch` (manual run) and deletes artifacts older than a configured threshold (default 30 days).

- The job is conservative: it skips artifact names listed in the `skipNames` whitelist (currently `DeDuBa-release-all` and `DeDuBa-release-staging`) to avoid removing release metadata or consolidated release artifacts.

## Safety checklist before running cleanup

- Confirm there are no ongoing artifact consumers (downloads in progress for the last 24 hours).
- Always whitelist artifacts used for release staging or persistent storage (e.g., 'DeDuBa-release-all').
- If performing cleanup manually, use at least 30 days as threshold by default.

## When to contact GitHub Support

- If storage usage doesn't drop after deletion or you cannot delete artifacts due to permission errors, contact GitHub Support (with repo admin) and request artifact quota increase or cleanup assistance.

## Example ad-hoc usage (one-off)

```bash
# List artifacts older than 60 days:
scripts/cleanup-artifacts-gh.sh 60 khmseu/deduba_cs

# Approved deletion: type YES to confirm
```

## Notes & maintenance

- The CI `artifact-cleanup` job is scheduled daily; consider adjusting `DAYS_TO_KEEP` in the job to a different value if your team requires more or less retention.
- Do not set `DAYS_TO_KEEP` lower than 7 unless you know that artifacts are not needed anymore for debugging or compliance.
