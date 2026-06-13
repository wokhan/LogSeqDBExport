using System.Net;
using System.Text.Json;

namespace LogSeqDBExport.Helpers;

/// <summary>
/// Parses Logseq/DB JSON dump items and converts them into `SourceEntity` lists
/// and related schema information used by the rest of the pipeline.
/// </summary>
public static class LogseqJsonParser
{
    sealed record RootItem(string content);

    public static List<SourceEntity> ParseEntitiesFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return ParseEntities(json, out var schema);
    }

    public static List<SourceEntity> ParseEntities(string json, out Dictionary<string, DBIdent> schema)
    {
        var rootItems = JsonSerializer.Deserialize<List<RootItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        var allEntities = new List<SourceEntity>();


        // foreach (var item in rootItems)
        // {
        //     var normalizedContent = WebUtility.HtmlDecode(item.content);
        //     var doc = JsonDocument.Parse(normalizedContent);
        //     resolver.InitCache(doc.RootElement);
        // }

        // var decodedRoot = (List<object>)((List<object>)DecodeItem(rootItems[0], resolver)!)[2];
        // var t = decodedRoot.Select((it, i) => (it, i))
        //            .Where(x => x.it is string key && key.StartsWith("~:"))
        //            //.DistinctBy(s => s.it)
        //            .ToLookup(x => (string)decodedRoot[x.i], x =>  DBIdent.CreateFromSchemaEntry(decodedRoot[x.i + 1]));

        // if (!TryGetInnerArray(decodedRoot, out var schemaArray, "~:schema"))
        // {
        //     Console.WriteLine("Unable to decode schema, aborting.");
        // }

        //schemaArray.ToDictionary(sa => sa[0], sa => sa[1]);

        foreach (var item in rootItems)
        {
            if (string.IsNullOrWhiteSpace(item.content))
            {
                continue;
            }

            var decoded = DecodeItem(item);

            if (TryGetInnerArray(decoded, out var keysArray))
            {
                allEntities.AddRange(keysArray.Select(MapToEntity).Where(e => e is not null)!);
            }
        }

        allEntities = allEntities.DistinctBy(e => (e.Id, e.Name, JsonSerializer.Serialize(e.Value), e.Transaction)).ToList();

        schema = allEntities.GroupBy(e => e.Id)
                            .Where(e => e.Any(ev => ev.Name == "~:db/ident"))
                            .ToDictionary(g => (string)g.First(ev => ev.Name == "~:db/ident")!.Value!, DBIdent.FromSourceEntities);
        schema.Add("_default", new DBIdent("", true, false, x => x));

        return allEntities;
    }

    private static object? DecodeItem(RootItem item)
    {
        var normalizedContent = WebUtility.HtmlDecode(item.content);
        var doc = JsonDocument.Parse(normalizedContent);

        // Resolve references ^1, ^2, ^>, ^@, ...
        // TODO: why a new resolver for each item?? missing the whole caching usefulness?
        var resolver = new TransitLikeResolver();
        return resolver.Decode(doc.RootElement);
    }

    private static bool TryGetInnerArray(object? decodedRoot, out List<List<object?>> innerArray, string key = "~:keys")
    {

        if (decodedRoot is List<object?> rootList
            && rootList.Count >= 3
            && key.Equals(rootList[1])
            && rootList[2] is List<object?> outerArray)
        {
            innerArray = [.. outerArray.OfType<List<object?>>()];
            return true;
        }

        innerArray = [];
        return false;
    }

    private static SourceEntity? MapToEntity(List<object?> row)
    {
        if (row.Count < 4)
        {
            Console.WriteLine("Ignored row: " + row.ToString());
            return null;
        }

        return new SourceEntity((double)row[0]!, row[1]!.ToString() ?? "", row[2], (long)(double)row[3]!);
    }

}
