using UtilitiesLibrary;

namespace DeDuBa;

// ReSharper disable once ClassNeverInstantiated.Global
internal class Program
{
    private static void Main(string[] args)
    {
        Utilities.Testing = true;
        DedubaClass.Backup(args);
    }
}