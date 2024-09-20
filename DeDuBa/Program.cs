namespace DeDuBa;

// ReSharper disable once ClassNeverInstantiated.Global
internal class Program
{
    private static void Main(string[] args)
    {
        DedubaClass.Testing = true;
        DedubaClass.Backup(args);
    }
}