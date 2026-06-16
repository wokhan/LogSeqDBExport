namespace LogSeqDBExport;

/// <summary>
/// Command-line options for the extraction tool (database path, output
/// directory, query/table selection and other flags).
/// </summary>
internal sealed record Options(string DbPath, string OutDir, string Table, string? Query, bool IncludeBlockProps, bool IgnoreBuiltIn, bool ExportOnlyPageChildren, string? IntermediateFile, string? DbOutputFile, string? FinalFile, bool UseTypeForFolder, bool ResolveAliases)
{

    public static Options Parse(string[] args)
    {
        string db = "", outDir = "", table = "kvs";
        string? query = null;
        bool resolveAliases = true;
        bool includeBlockProps = true;
        bool ignoreBuiltIn = false;
        bool exportOnlyPageChildren = false;
        bool useTypeForFolder = false;
        string? intermediateFile = null;
        string? dbOutputFile = null;
        string? finalFile = null;

        if (args.Any(arg => arg == "-h" || arg == "--help"))
        {
            PrintUsage(null, null);
            Environment.Exit(0);
        }

        foreach (var chunk in args.Chunk(2))
        {
            string a = chunk[0];
            string next = chunk[1];

            switch (a)
            {
                case "--db": db = next; break;
                case "--out": outDir = next; break;
                case "--resolveAliases": resolveAliases = GetBoolean(next, resolveAliases); break;
                case "--table": table = next; break;
                case "--query": query = next; break;
                case "--includeBlockProps": includeBlockProps = GetBoolean(next, includeBlockProps); break;
                case "--ignoreBuiltIn": ignoreBuiltIn = GetBoolean(next, ignoreBuiltIn); break;
                case "--intermediateFile": intermediateFile = next; break;
                case "--dboutputFile": dbOutputFile = next; break;
                case "--finalFile": finalFile = next; break;
                case "--exportOnlyPageChildren": exportOnlyPageChildren = GetBoolean(next, exportOnlyPageChildren); break;
                case "--useTypeForFolder": useTypeForFolder = GetBoolean(next, useTypeForFolder); break;
            }
        }

        if (string.IsNullOrWhiteSpace(db) || string.IsNullOrWhiteSpace(outDir))
        {
            PrintUsage(db, outDir);
            Environment.Exit(2);
        }

        return new Options(db, outDir, table, query, includeBlockProps, ignoreBuiltIn, exportOnlyPageChildren, intermediateFile, dbOutputFile, finalFile, useTypeForFolder, resolveAliases);
    }

    private static bool GetBoolean(string next, bool def)
    {
        return bool.TryParse(next, out var ra) ? ra : def;
    }

    private static void PrintUsage(string? db, string? outDir)
    {
        if (string.IsNullOrWhiteSpace(db))
        {
            Console.Error.WriteLine("Missing required option: --db <sqlite-file>");
        }

        if (string.IsNullOrWhiteSpace(outDir))
        {
            Console.Error.WriteLine("Missing required option: --out <output-dir>");
        }

        Console.Error.WriteLine();
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  LogSeqDBExport --db <sqlite-file> --out <output-dir> [options]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Options:");
        Console.Error.WriteLine("  --db <sqlite-file>                Path to the Logseq SQLite database.");
        Console.Error.WriteLine("  --out <output-dir>                Output directory for generated files.");
        Console.Error.WriteLine("  --table <table-name>              Table to read (default: kvs).");
        Console.Error.WriteLine("  --query <sql>                     Optional SQL query to override the default select.");
        Console.Error.WriteLine("  --includeBlockProps true|false    Include block properties in output (default: true).");
        Console.Error.WriteLine("  --ignoreBuiltIn true|false        Ignore built-in properties (default: false).");
        Console.Error.WriteLine("  --exportOnlyPageChildren true|false  Export only page direct children (not transitive ones) (default: false).");
        Console.Error.WriteLine("  --useTypeForFolder true|false     Create folders by type when exporting pages (default: false).");
        Console.Error.WriteLine("  --intermediateFile <file>         Write intermediate JSON to file.");
        Console.Error.WriteLine("  --dboutputFile <file>             Write raw DB export to file.");
        Console.Error.WriteLine("  --finalFile <file>                Write final JSON output to file.");
        Console.Error.WriteLine("  -h, --help                        Show this help message.");
    }
}
