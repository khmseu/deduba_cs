namespace UtilitiesLibrary;

/// <summary>
/// Represents configuration settings used by the backup utilities.
/// Implementations provide immutable configuration values consumed by
/// backup components.
/// </summary>
public interface IBackupConfig
{
    /// <summary>
    /// The root directory where archives are stored.
    /// </summary>
    string ArchiveRoot { get; init; }

    /// <summary>
    /// The path to the data directory being backed up.
    /// </summary>
    string DataPath { get; init; }

    /// <summary>
    /// Size of chunks (in bytes) used when splitting large files.
    /// </summary>
    long ChunkSize { get; init; }

    /// <summary>
    /// Indicates whether the backup system is running in testing mode.
    /// </summary>
    bool Testing { get; init; }

    /// <summary>
    /// When <c>true</c>, enables verbose logging and diagnostic output.
    /// </summary>
    bool Verbose { get; init; }

    /// <summary>
    /// Threshold for splitting prefixes; values above this trigger prefix splitting.
    /// </summary>
    int PrefixSplitThreshold { get; init; }

    /// <summary>
    /// Create an instance of <see cref="BackupConfig"/> populated from
    /// utility/environment settings.
    /// </summary>
    /// <returns>A <see cref="BackupConfig"/> instance representing current utilities configuration.</returns>
    static abstract BackupConfig FromUtilities();
}
