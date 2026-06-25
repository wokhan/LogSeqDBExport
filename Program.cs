using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using LogSeqDBExport.Helpers;
using LogSeqDBExport.Models;

namespace LogSeqDBExport;

/// <summary>
/// Application entry point that orchestrates reading the database, parsing
/// the exported JSON, transforming entities and exporting them to Markdown.
/// </summary>
internal static partial class Program
{
    static Config config = null!;
    static readonly JsonSerializerOptions serializerOptions = new() { WriteIndented = true };

    public static int Main(string[] args)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        var options = Options.Parse(args);

        Directory.CreateDirectory(options.OutDir);

        config = Config.ReadFrom("config/config.json");
        config.EnsureDefaultMappings(!options.ResolveAliases);

        var rawData = DBHelper.ReadFromDb(options);

        if (options.DbOutputFile is not null)
        {
            File.WriteAllText(Path.Combine(options.OutDir, options.DbOutputFile), rawData);
        }

        var sourceEntities = LogseqJsonParser.ParseSourceEntries(rawData);
        
        var entitiesById = EntitiesManager.FromSourceEntries(sourceEntities, out var schema);

        if (options.IntermediateFile is not null)
        {
            File.WriteAllText(Path.Combine(options.OutDir, options.IntermediateFile), JsonSerializer.Serialize(entitiesById, serializerOptions));
        }

        DumpUserProperties(entitiesById);

        Console.WriteLine("Mapping properties and resolving references...");
        EntitiesManager.ResolveAndMapProperties(entitiesById, schema, options, config);

        if (options.FinalFile is not null)
        {
            File.WriteAllText(Path.Combine(options.OutDir, options.FinalFile), JsonSerializer.Serialize(entitiesById, serializerOptions));
        }

        Console.WriteLine("Rendering pages...");
        RenderHelper.RenderPages(options, entitiesById.Values, config);

        return 0;
    }

    private static void DumpUserProperties(Dictionary<double, Entity> entitiesById)
    {
        var userprops = entitiesById.SelectMany(e => e.Value.RawProperties.Where(prop => prop.Key.StartsWith("~:user.property/")).Select(e => e.Key))
                                    .Distinct()
                                    .Order()
                                    .ToDictionary(key => key, config.Mappings.ContainsKey);

        using var stream = Console.OpenStandardOutput();
        JsonSerializer.Serialize(stream, userprops, serializerOptions);

        Console.WriteLine();
    }
}
