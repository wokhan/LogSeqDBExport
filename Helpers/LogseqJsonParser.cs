using System.Net;
using System.Text.Json;
using LogSeqDBExport.Models;

namespace LogSeqDBExport.Helpers;

/// <summary>
/// Parses Logseq/DB JSON dump items and converts them into `SourceEntity` lists
/// and related schema information used by the rest of the pipeline.
/// </summary>
public static class LogseqJsonParser
{
    sealed record RootItem(string content);

    public static List<SourceEntry> ParseEntitiesFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return ParseSourceEntries(json);
    }

    public static List<SourceEntry> ParseSourceEntries(string json)
    {
        var rootItems = JsonSerializer.Deserialize<List<RootItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        var allEntities = new List<SourceEntry>();

        foreach (var item in rootItems)
        {
            if (string.IsNullOrWhiteSpace(item.content))
            {
                continue;
            }

            var decoded = DecodeItem(item);

            if (TryGetInnerArray(decoded, out var keysArray))
            {
                allEntities.AddRange(keysArray.Select(FromRow).Where(e => e is not null)!);
            }
        }

        allEntities = allEntities.DistinctBy(e => (e.Id, e.Name, JsonSerializer.Serialize(e.Value), e.Transaction)).ToList();

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

    private static SourceEntry? FromRow(List<object?> row)
    {
        if (row.Count < 4)
        {
            Console.WriteLine("Ignored row: " + row.ToString());
            return null;
        }

        return new SourceEntry((double)row[0]!, row[1]!.ToString() ?? "", row[2], (double)row[3]!);
    }
}
