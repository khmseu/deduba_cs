# Future Plans / Distribution Roadmap

This document captures near-term and mid-term enhancements for packaging, distribution, and release automation of DeDuBa.

## 1. CI Integration

Planned steps:

- Add CI workflow to build and test on Linux (native) and cross-build Windows shim.
- Use matrix for `Debug` (smoke) and `Release` (publish) builds.
- Cache `~/.nuget/packages` and native build intermediates to speed up runs.
- Fail fast on native shim build errors; surface `DllNotFoundException` root cause hints.

## 2. Artifact Upload

- Upload versioned archives: `DeDuBa-<version>-<rid>.tar.gz` and `.zip` to CI run as build artifacts.
- Optionally include symbol files (`*.pdb`) and XML docs for developer builds.
- Add release job that triggers on tag `v*` pushes; promotes artifacts to GitHub Release with generated changelog.

## 3. Checksums & Integrity

- Generate SHA-512 checksum files alongside archives: `DeDuBa-<version>-<rid>.tar.gz.sha512`.
- Provide verification snippet in README (`shasum -a 512 -c`).
- Future: Signed checksums (GPG) if distribution extends beyond internal use.

## 4. Cleanup Strategy

- Current approach: manual removal of legacy non-versioned `dist/DeDuBa-<rid>` artifacts.
- Future enhancement: `scripts/package.sh clean` mode to prune outdated versioned artifacts (retain last N).
- Add CI job to auto-delete stale artifacts older than X days on non-release branches.

## 5. Windows Cross-Build Validation

- Add automated smoke test invoking published `DeDuBa.exe` under Wine (optional) to verify startup and error reporting.
- Future: native Windows runner in separate workflow using hosted Windows agents.

## 6. Release Automation

- Use MinVer for version; ensure tags follow `v<semver>` format.
- Auto-generate changelog section from Conventional Commits between last and current tag.
- Post changelog to release notes; attach artifacts and checksums.

## 7. Extended Packaging

- Provide container image (`ghcr.io/<org>/deduba:<version>`) with runtime dependencies.
- Add Homebrew tap formula / winget manifest as optional distribution channels.

## 8. Observability Hooks (Optional)

- Runtime metrics export (processed bytes, dedupe ratio) via stdout JSON lines or Prometheus endpoint.
- Structured log ingestion pipeline for long-running archival tasks.

## 9. Security & Hardening

- SBOM generation (CycloneDX) during publish.
- Binary signing (Linux: minisign; Windows: signtool) for official releases.

## 10. Backlog Summary

| Area               | Status   | Priority |
| ------------------ | -------- | -------- |
| CI build matrix    | Pending  | High     |
| Artifact upload    | Pending  | High     |
| Checksums          | Pending  | High     |
| Cleanup script     | Deferred | Low      |
| Wine smoke test    | Pending  | Medium   |
| Release automation | Pending  | High     |
| Container image    | Idea     | Medium   |
| Brew/winget        | Idea     | Low      |
| Metrics export     | Idea     | Low      |
| SBOM & signing     | Idea     | Medium   |

## 11. Immediate Next Steps (When Prioritized)

1. Add GitHub Actions workflow (`ci.yml`) for Linux build/test + versioned packaging.
2. Add checksum generation in `scripts/package.sh` (optional flag `WITH_CHECKSUM=1`).
3. Implement release workflow triggered by tags.

---

Document created: 2025-11-16
