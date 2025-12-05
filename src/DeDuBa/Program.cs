using UtilitiesLibrary;

namespace DeDuBa;

// ReSharper disable once ClassNeverInstantiated.Global
/// <summary>
///     Entry point for the DeDuBa command-line application.
///     Parses command-line options and initiates the backup process.
/// </summary>
internal class Program
{
    /// <summary>
    ///     Main entry point that parses command-line arguments and invokes the backup worker.
    /// </summary>
    /// <param name="args">Command-line arguments including file paths and options (--verbose, --production, --help).</param>
    private static void Main(string[] args)
    {
        Utilities.Testing = true; // Default to testing mode

        // Parse command-line options
        var fileArgs = new List<string>();
        foreach (var arg in args)
            if (arg == "--verbose" || arg == "-v")
            {
                Utilities.VerboseOutput = true;
            }
            else if (arg == "--production" || arg == "-p")
            {
                Utilities.Testing = false;
            }
            else if (arg == "--help" || arg == "-h")
            {
                ShowHelp();
                Environment.Exit(0);
            }
            else
            {
                fileArgs.Add(arg);
            }

        DedubaClass.Backup([.. fileArgs]);
    }

    /// <summary>
    ///     Displays usage information and available command-line options to the console.
    /// </summary>
    private static void ShowHelp()
    {
        Console.WriteLine("DeDuBa - Deduplicating Backup System");
        Console.WriteLine("Usage: DeDuBa [options] <files-to-backup>");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -v, --verbose      Enable verbose diagnostic output");
        Console.WriteLine("  -p, --production   Use production archive path (/archive/backup)");
        Console.WriteLine("                     Default: test mode (~/projects/Backup/ARCHIVE5)");
        Console.WriteLine("  -h, --help         Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  DeDuBa /tmp                    # Backup /tmp to test archive");
        Console.WriteLine("  DeDuBa --verbose /home/user    # Backup with diagnostic output");
        Console.WriteLine("  DeDuBa --production /data      # Backup to production archive");
    }
}
