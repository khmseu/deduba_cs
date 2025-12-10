using System.Collections.Generic;

namespace Interfaces
{
    public interface IHighLevelOsApi
    {
        IEnumerable<string> ReadDirectory(string path);
        bool IsDirectory(string path);
    }
}
