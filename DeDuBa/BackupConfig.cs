using UtilitiesLibrary;
#pragma warning disable CS1591
using System;
using System.IO;

namespace DeDuBa;

public sealed class BackupConfig
{
    public string ArchiveRoot { get; init; }
    public string DataPath { get; init; }
    public long ChunkSize { get; init; } = 1024 * 1024 * 1024;
    public bool Testing { get; init; }
    public bool Verbose { get; init; }
    public int PrefixSplitThreshold { get; init; } = 255;

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

    public static BackupConfig FromUtilities()
    {
        var testing = Utilities.Testing;
        var archiveRoot = testing ? "/home/kai/projects/Backup/ARCHIVE4" : "/archive/backup";
        var verbose = Utilities.VerboseOutput;
        return new BackupConfig(archiveRoot, 1024L * 1024L * 1024L, testing, verbose, 255);
    }
}
