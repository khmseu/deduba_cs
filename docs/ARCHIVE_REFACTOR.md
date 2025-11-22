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

## Testing notes

- Tests are included for `BuildIndex`, `SaveData`, `SaveStream`, and `Reorg`.
- Integration tests validate `DedubaClass` calling `ArchiveStore`.
- Tests should clean up temp directories on success/failure.

## Observations

- The reorganization logic is conservative — moving files into new directories when a prefix grows above `PrefixSplitThreshold`.
- The ArchiveStore design will help to implement future features like compaction and index persistence without touching `DedubaClass` traversal logic.
