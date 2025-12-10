namespace ArchiveDataHandler;

/// <summary>
///     Interface for content-addressable archive storage with deduplication.
///     Manages hash-indexed data blocks and provides transparent deduplication via SHA-512 hashing.
/// </summary>
public interface IArchiveStore
{
    /// <summary>
    ///     Gets a dictionary mapping hash values to their storage prefix paths.
    ///     Key: hex-encoded SHA-512 hash, Value: relative prefix path from DATA directory.
    /// </summary>
    IReadOnlyDictionary<string, string> Arlist { get; }

    /// <summary>
    ///     Gets a dictionary mapping prefix paths to collections of child entries.
    ///     Key: prefix path, Value: collection of file hashes or subdirectory names in that prefix.
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> Preflist { get; }

    /// <summary>
    ///     Gets statistics about archive operations (saved_blocks, saved_bytes, duplicate_blocks, duplicate_bytes).
    /// </summary>
    IReadOnlyDictionary<string, long> Stats { get; }

    /// <summary>
    ///     Gets the cumulative size in bytes of data stored or deduplicated in the current session.
    /// </summary>
    long PackSum { get; }

    /// <summary>
    ///     Gets the absolute path to the DATA directory where content chunks are stored.
    /// </summary>
    string DataPath { get; }

    /// <summary>
    ///     Static factory property that returns a default singleton instance of an archive store.
    ///     Implementations must provide a matching static property returning an `IArchiveStore`.
    /// </summary>
    static abstract IArchiveStore Instance { get; }

    /// <summary>
    ///     Scans the DATA directory and populates the hash and prefix indexes.
    ///     Should be called once before performing save operations.
    /// </summary>
    void BuildIndex();

    /// <summary>
    ///     Maps a content hash to its target storage path, creating directories as needed.
    ///     Returns null if the hash already exists (deduplication hit).
    /// </summary>
    /// <param name="hexHash">Hex-encoded SHA-512 hash of the content.</param>
    /// <returns>Absolute file path for new content, or null if already stored.</returns>
    string? GetTargetPathForHash(string hexHash);

    /// <summary>
    ///     Hashes data with SHA-512, compresses with BZip2, and stores if not already present.
    /// </summary>
    /// <param name="data">Raw data bytes to store.</param>
    /// <returns>Hex-encoded SHA-512 hash of the data.</returns>
    string SaveData(ReadOnlySpan<byte> data);

    /// <summary>
    ///     Reads a stream in chunks, hashes and stores each chunk, and returns the list of chunk hashes.
    /// </summary>
    /// <param name="stream">Source stream to read from.</param>
    /// <param name="size">Expected size in bytes to read from the stream.</param>
    /// <param name="tag">Descriptive tag for logging and progress reporting.</param>
    /// <param name="progress">Optional callback invoked with bytes processed for progress tracking.</param>
    /// <returns>List of hex-encoded SHA-512 hashes for each chunk.</returns>
    List<string> SaveStream(Stream stream, long size, string tag, Action<long>? progress = null);
}