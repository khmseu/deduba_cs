using System.IO;

namespace Interfaces
{
    public interface IArchiveStore
    {
        void BuildIndex();
        string GetTargetPathForHash(string hash);
        string SaveData(byte[] data);
        string SaveStream(Stream s);
    }
}
