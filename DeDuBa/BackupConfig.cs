using UtilitiesLibrary;

namespace DeDuBa;

/// <summary>
///     Configuration settings for the backup archive system.
///     Encapsulates archive root path, chunk size, and operational flags.
/// </summary>
public sealed class BackupConfig
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="BackupConfig" /> class.
    /// </summary>
    /// <param name="archiveRoot">Root directory path for the backup archive.</param>
    /// <param name="chunkSize">Size in bytes for content-addressed chunks (default: 1GB).</param>
    /// <param name="testing">Whether to run in testing mode (affects archive location).</param>
    /// <param name="verbose">Whether to enable verbose diagnostic output.</param>
    /// <param name="prefixSplitThreshold">
    ///     Maximum entries per prefix directory before triggering reorganization (default:
    ///     255).
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="archiveRoot" /> is null.</exception>
    public BackupConfig(
        string archiveRoot,
        long chunkSize = 1024L * 1024L * 1024L,
        bool testing = false,
        bool verbose = false,
        int prefixSplitThreshold = 255
    )
    {
        ArchiveRoot = archiveRoot ?? throw new ArgumentNullException(nameof(archiveRoot));
        DataPath = Path.Combine(ArchiveRoot, "DATA");
        ChunkSize = chunkSize;
        Testing = testing;
        Verbose = verbose;
        PrefixSplitThreshold = prefixSplitThreshold;
    }

    /// <summary>
    ///     Gets the root directory path for the backup archive.
    /// </summary>
    public string ArchiveRoot { get; init; }

    /// <summary>
    ///     Gets the DATA subdirectory path where content-addressed chunks are stored.
    /// </summary>
    public string DataPath { get; init; }

    /// <summary>
    ///     Gets the maximum size in bytes for each content chunk (default: 1GB).
    /// </summary>
    public long ChunkSize { get; init; } = 1024 * 1024 * 1024;

    /// <summary>
    ///     Gets a value indicating whether the system is running in testing mode.
    ///     Testing mode uses a local archive path instead of the production path.
    /// </summary>
    public bool Testing { get; init; }

    /// <summary>
    ///     Gets a value indicating whether verbose diagnostic output is enabled.
    /// </summary>
    public bool Verbose { get; init; }

    /// <summary>
    ///     Gets the maximum number of entries allowed in a prefix directory before
    ///     triggering automatic reorganization into subdirectories (default: 255).
    /// </summary>
    public int PrefixSplitThreshold { get; init; } = 255;

    /// <summary>
    ///     Creates a <see cref="BackupConfig" /> instance using current <see cref="Utilities" /> settings.
    /// </summary>
    /// <returns>A new <see cref="BackupConfig" /> initialized from global utility settings.</returns>
    public static BackupConfig FromUtilities()
    {
        var testing = Utilities.Testing;
        // Prefer explicit override if present (CI and local scripts can set this)
        var envArchiveRoot = Environment.GetEnvironmentVariable("DEDU_ARCHIVE_ROOT");
        string archiveRoot;
        if (!string.IsNullOrEmpty(envArchiveRoot))
            archiveRoot = envArchiveRoot;
        else if (testing)
            // Use a workspace-local / tmp directory in testing mode so CI and local runs don't attempt to create paths under /home/kai
            archiveRoot = Path.Combine(Path.GetTempPath(), "ARCHIVE4");
        else
            archiveRoot = "/archive/backup";
        var verbose = Utilities.VerboseOutput;
        return new BackupConfig(archiveRoot, 1024L * 1024L * 1024L, testing, verbose);
    }
}