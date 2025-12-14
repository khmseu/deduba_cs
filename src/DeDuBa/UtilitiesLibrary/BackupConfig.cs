namespace UtilitiesLibrary;

/// <summary>
///     Configuration settings for the backup archive system.
///     Encapsulates archive root path, chunk size, and operational flags.
/// </summary>
public sealed class BackupConfig : IBackupConfig
{
    private static IBackupConfig? _instance;
    private static readonly object _instanceLock = new();

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
    ///     Default singleton instance of <see cref="IBackupConfig" />.
    ///     Throws if not initialized via <see cref="SetInstance" />.
    /// </summary>
    public static IBackupConfig Instance =>
        _instance
        ?? throw new InvalidOperationException(
            "BackupConfig.Instance not initialized. Call BackupConfig.SetInstance(...) before use."
        );

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
    ///     Set the global BackupConfig instance. Can only be called once.
    /// </summary>
    /// <exception cref="ArgumentNullException" />
    /// <exception cref="InvalidOperationException">If already set.</exception>
    public static void SetInstance(IBackupConfig instance)
    {
        if (instance is null)
            throw new ArgumentNullException(nameof(instance));

        lock (_instanceLock)
        {
            if (_instance is not null)
                throw new InvalidOperationException("BackupConfig instance already set.");
            _instance = instance;
        }
    }

    /// <summary>
    ///     Initialize the global BackupConfig singleton from utilities, optionally overriding the archive root.
    /// </summary>
    public static void InitializeFromUtilitiesWithOverride(string? overrideArchiveRoot)
    {
        lock (_instanceLock)
        {
            if (_instance is null)
            {
                _instance = FromUtilitiesWithOverride(overrideArchiveRoot);
                return;
            }

            // If caller provides an explicit override archive root, prefer that
            // and replace the current instance when the roots differ. This allows
            // runtime initialization (which may pass an explicit archive path)
            // to override an instance previously created by test fixtures.
            if (!string.IsNullOrEmpty(overrideArchiveRoot) && _instance.ArchiveRoot != overrideArchiveRoot)
                _instance = FromUtilitiesWithOverride(overrideArchiveRoot);
        }
    }

    /// <summary>
    ///     Create an effective <see cref="BackupConfig" /> based on current utilities, but allow
    ///     optionally overriding the archive root. This replicates the previous two-step
    ///     InitializeBackupConfig behaviour used by the main program.
    /// </summary>
    /// <param name="overrideArchiveRoot">If non-null, use this as the archive root instead of the utilities-derived value.</param>
    /// <returns>A new <see cref="BackupConfig" /> with the effective settings.</returns>
    public static BackupConfig FromUtilitiesWithOverride(string? overrideArchiveRoot)
    {
        // Derive base values from Utilities (same logic previously in FromUtilities)
        var testing = Utilities.Testing;
        var envArchiveRoot = Environment.GetEnvironmentVariable("DEDU_ARCHIVE_ROOT");
        string baseArchiveRoot;
        if (!string.IsNullOrEmpty(envArchiveRoot))
            baseArchiveRoot = envArchiveRoot;
        else if (testing)
            baseArchiveRoot = Path.Combine(Path.GetTempPath(), "ARCHIVE5");
        else
            baseArchiveRoot = "/archive/backup";

        var verbose = Utilities.VerboseOutput;
        var chunkSize = 1024L * 1024L * 1024L;
        var prefixSplitThreshold = 255;

        var archiveRoot = !string.IsNullOrEmpty(overrideArchiveRoot) ? overrideArchiveRoot : baseArchiveRoot;

        return new BackupConfig(archiveRoot, chunkSize, testing, verbose, prefixSplitThreshold);
    }

    /// <summary>
    ///     Backwards-compatible helper that returns the utilities-derived config (no override).
    /// </summary>
    public static BackupConfig FromUtilities()
    {
        return FromUtilitiesWithOverride(null);
    }
}
