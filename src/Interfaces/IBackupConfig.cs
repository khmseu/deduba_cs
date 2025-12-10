namespace Interfaces
{
    public interface IBackupConfig
    {
        string ArchiveRoot { get; }
        string DataPath { get; }
        int ChunkSize { get; }
        bool Testing { get; }
        bool Verbose { get; }
        long PrefixSplitThreshold { get; }
    }
}
