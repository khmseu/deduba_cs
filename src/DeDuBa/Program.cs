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
        // Ensure the program-level logger is available (defaults already set in DedubaClass)
        DedubaClass.Logger = new UtilitiesLibrary.UtilitiesLogger();

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
        DedubaClass.Logger.ConWrite("DeDuBa - Deduplicating Backup System");
        DedubaClass.Logger.ConWrite("Usage: DeDuBa [options] <files-to-backup>");
        DedubaClass.Logger.ConWrite("");
        DedubaClass.Logger.ConWrite("Options:");
        DedubaClass.Logger.ConWrite("  -v, --verbose      Enable verbose diagnostic output");
        DedubaClass.Logger.ConWrite(
            "  -p, --production   Use production archive path (/archive/backup)"
        );
        DedubaClass.Logger.ConWrite(
            "                     Default: test mode (~/projects/Backup/ARCHIVE5)"
        );
        DedubaClass.Logger.ConWrite("  -h, --help         Show this help message");
        DedubaClass.Logger.ConWrite("");
        DedubaClass.Logger.ConWrite("Examples:");
        DedubaClass.Logger.ConWrite(
            "  DeDuBa /tmp                    # Backup /tmp to test archive"
        );
        DedubaClass.Logger.ConWrite(
            "  DeDuBa --verbose /home/user    # Backup with diagnostic output"
        );
        DedubaClass.Logger.ConWrite(
            "  DeDuBa --production /data      # Backup to production archive"
        );
    }
}
