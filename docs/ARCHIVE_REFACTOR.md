# ArchiveStore Refactor Notes & Improvements

This document summarizes the refactor to extract archive/data management to `ArchiveStore`, the new design choices, and recommended improvements.

## What moved

- `Arlist`, `Preflist` state and management
- `Mkarlist` -> `ArchiveStore.BuildIndex`
- `Hash2Fn` -> `ArchiveStore.GetTargetPathForHash`
- `Save_data` -> `ArchiveStore.SaveData`
- `Save_file` -> `ArchiveStore.SaveStream`
- Helper functions related to directory management are now internal to `ArchiveStore`.

## Design choices

- `BackupConfig` centralizes configuration (`ArchiveRoot`, `DataPath`, `ChunkSize`, `PrefixSplitThreshold`, `Testing`, `Verbose`).
- `IArchiveStore` interface for testability and DI.
- `ArchiveStore` uses `ConcurrentDictionary` and careful locking for reorganization.
- `Preflist` stores a `HashSet<string>` per prefix rather than \0-delimited strings.

## Future improvements & follow-ups

- Add async variants: `SaveStreamAsync`.
- Replace `HashSet` in `Preflist` with `ConcurrentHashSet` or concurrent-safe structure for highly concurrent code.
- Persist `Arlist`/`Preflist` to a small on-disk index (e.g., JSON or an embedded DB) to avoid `BuildIndex()` long re-index times.
- Add GC for unused chunks (compaction).
- Consider writing compressed files to temporary path and `File.Move` for atomic writes.
- Add `IArchiveLogger` or `ILogger` for better logging and tracing.
- Integrate Prometheus-style metrics for saved/duplicate blocks.
 - Add CLI tooling for inspecting the archive, listing prefixes, and restore operations.
 - Add transactional and inter-process safety (e.g., file locks or leader election) for multi-process writes against the same archive to avoid races.
 - Improve BuildIndex performance by persisting an index on disk and using change journals or file monitoring to update it incrementally.
 - Implement snapshot exports and compaction (dedup/garbage-collect) to reduce archive size over time.
 - Add end-to-end restore/integrity verification tests (read-back written chunks and verify checksums and metadata round-trip).
 - Add retention and pruning policy: allow the system to remove file chunks no longer referenced by any inode (safe compaction algorithm).
 - Add a safe migration path for existing archives: utilities to import/export index formats, and tools to convert prefix formats.
 - Add more comprehensive concurrency tests: simultaneous save streams, reorg under load, conflicts when one writer reorganizes a directory.
 - Add support for partial uploads and resumable chunking: allow saving to resume interrupted uploads.
 - Consider adding encryption-at-rest options (AES-GCM) and key rotation features.
 - Add support for verification / digest-only mode to quickly scan archive for missing or corrupted files.
 - Add metadata snapshots for archiving and quick restore of directory structures.

## Testing notes

- Tests are included for `BuildIndex`, `SaveData`, `SaveStream`, and `Reorg`.
- Integration tests validate `DedubaClass` calling `ArchiveStore`.
- Tests should clean up temp directories on success/failure.

## Observations

- The reorganization logic is conservative — moving files into new directories when a prefix grows above `PrefixSplitThreshold`.
- The ArchiveStore design will help to implement future features like compaction and index persistence without touching `DedubaClass` traversal logic.
