namespace DeDuBa;

// ReSharper disable once ClassNeverInstantiated.Global
internal class Program
{
    private static void Main(string[] args)
    {
        UtilitiesLibrary.Utilities.Testing = true;
        DedubaClass.Backup(args);
    }
}