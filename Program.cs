using System.Text.Json;
using System.Text.RegularExpressions;
using LogSeqDBExport.Helpers;
using Microsoft.Data.Sqlite;

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
        var opt = Options.Parse(args);

        Directory.CreateDirectory(opt.OutDir);

        config = Config.ReadFrom("config/config.json");

        var rawData = ReadFromDb(opt);

        if (opt.DbOutputFile is not null)
        {
            File.WriteAllText(Path.Combine(opt.OutDir, opt.DbOutputFile), rawData);
        }

        var sourceEntities = LogseqJsonParser.ParseEntities(rawData, out var schema);

        var test = sourceEntities.Where(a => a.Name == "~:block/created-at" && false.Equals(a.Value)).ToList();

        var entitiesById = sourceEntities.GroupBy(e => e.Id)
                                         .Where(e => !e.Any(it => it.Name == "~:logseq.property/deleted-at" || (it.Name == "~:block/title" && config.Exclusions.Contains(it.Value))))
                                         .ToDictionary(g => g.Key, g => new Entity(g.Key, g.GroupBy(it => it.Name).ToDictionary(it => it.Key, it => schema.GetValueOrDefault(it.Key, schema["_default"]).GetValue(it.Select(a => a.Value))), []));

        if (opt.IntermediateFile is not null)
        {
            File.WriteAllText(Path.Combine(opt.OutDir, opt.IntermediateFile), JsonSerializer.Serialize(entitiesById, serializerOptions));
        }

        DumpUserProperties(entitiesById);

        Console.WriteLine("Rebuilding hierarchy...");
        ResolveParents(entitiesById);

        Console.WriteLine("Sorting blocks...");
        SortEntities(entitiesById.Values);

        Console.WriteLine("Resolving references...");
        ResolveRefsAndUUIDs(entitiesById);

        Console.WriteLine("Mapping properties...");
        MapProperties(entitiesById);

        if (opt.FinalFile is not null)
        {
            File.WriteAllText(Path.Combine(opt.OutDir, opt.FinalFile), JsonSerializer.Serialize(entitiesById, serializerOptions));
        }

        Console.WriteLine("Rendering pages...");
        RenderHelper.RenderPages(opt, entitiesById, config);

        return 0;
    }

    private static void MapProperties(Dictionary<double, Entity> entitiesById)
    {
        foreach (var (id, entity) in entitiesById)
        {
            var propertyMappings = entity.IsRootExportable ? config.PagePropertyMappings : config.PropertyMappings;
            entity.FinalProperties = entity.Properties.Where(p => propertyMappings.ContainsKey(p.Key))
                                                      .ToDictionary(e => propertyMappings[e.Key], e => e.Value);

            foreach (var mapping in config.TagsToPropertyMappings)
            {
                if (entity.Properties.TryGetValue("~:block/tags", out var tags) && ((object[])tags!).Contains(mapping.Key))
                {
                    entity.FinalProperties.Add(mapping.Value[0], mapping.Value[1]);
                }
            }
        }
    }

    private static void DumpUserProperties(Dictionary<double, Entity> entitiesById)
    {
        var userprops = entitiesById.SelectMany(e => e.Value.Properties.Where(prop => prop.Key.StartsWith("~:user.property/")).Select(e => e.Key))
                                    .Distinct()
                                    .Order()
                                    .ToDictionary(key => key, config.PagePropertyMappings.ContainsKey);

        using var stream = Console.OpenStandardOutput();
        JsonSerializer.Serialize(stream, userprops, serializerOptions);

        Console.WriteLine();
    }

    private static void ResolveRefsAndUUIDs(Dictionary<double, Entity> entitiesById)
    {
        var entitiesByUUID = entitiesById.Where(e => e.Value.UUID != Entity.EmptyUUID)
                                         .ToDictionary(e => e.Value.UUID, e => e.Value);

        foreach (var (key, entity) in entitiesById)
        {
            foreach (var property in entity.Properties)
            {
                if (property.Key == "~:block/title" && property.Value is string strval)
                {
                    var step1 = ReplaceUuidLinks(strval, entitiesByUUID);
                    var step2 = FormatDateRef(step1);

                    entity.Properties[property.Key] = step2;
                }
                //TODO: update to check schema instead for resolvable props (should be defined somehow)
                else if (config.ResolvableProps.Contains(property.Key))
                {
                    entity.Properties[property.Key] = ResolveEntityRefs(property.Value, entitiesById);
                }
                //entity.Properties[property.Key] = ResolveIfNeeded(property.Key, property.Value, entitiesById, entitiesByUUID);
            }
            // var resolvedProperties = entity.Properties.SelectMany(look => look.Select(lookv => (look.Key, mappedProp: ResolveIfNeeded(look.Key, lookv, entitiesById, entitiesByUUID))))
            //                                           .ToLookup(t => t.Key, t => t.mappedProp);

            // entity.Properties = resolvedProperties;
            // //entitiesById[key] = entity with { Properties = resolvedProperties };
        }
    }

    private static object? FormatDateRef(string step1)
    {
        return config.DateMappingReg.Key.Replace(step1, config.DateMappingReg.Value);
    }

    private static void ResolveParents(Dictionary<double, Entity> entitiesById)
    {
        foreach (var entity in entitiesById.Values)
        {
            if (entity.Properties.TryGetValue("~:block/parent", out var parent) && parent is double parentId && entitiesById.TryGetValue(parentId, out var parentEntity))
            {
                parentEntity.Children.Add(entity);
            }
        }
    }


    private static void SortEntities(IEnumerable<Entity> entities)
    {
        foreach (var entity in entities)
        {
            entity.Children.Sort();
            //(a, b) =>
            // {
            //     var orderA = a.Properties["~:block/order"];
            //     var orderB = b.Properties["~:block/order"];

            //     if (orderA is string oa && orderB is string ob)
            //         return oa.CompareTo(ob, StringComparison.InvariantCulture);
            //     if (orderA is string) return -1;
            //     if (orderB is string) return 1;
            //     return 0;
            // });

            SortEntities(entity.Children);
        }
    }

    private static string ReadFromDb(Options opt)
    {
        string tmpjson;

        using var conn = new SqliteConnection($"Data Source={opt.DbPath}");
        conn.Open();

        var sql = opt.Query ?? $"select content from {opt.Table}";
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var reader = cmd.ExecuteReader();
        var sbx = new List<string>(100_000);

        while (reader.Read())
        {
            sbx.Add($"\t{{\n\t\"content\": \"{reader.GetString(0).Replace("\\", "\\\\").Replace("\"", "\\\"")}\"\n\t}}");
        }

        tmpjson = $"[{String.Join(",", sbx)}]";

        return tmpjson;
    }


    public static object? ResolveEntityRefs(object? value, Dictionary<double, Entity> entitiesById)
    {
        switch (value)
        {
            case long id:
                {
                    if (entitiesById.TryGetValue(id, out var e))
                    {
                        var val = e.DisplayTitle ?? id.ToString();
                        return e.IsTag ? $"#{val}" : $"[[{val}]]";
                    }
                    return value;
                }

            case double doubleId:
                {
                    if (entitiesById.TryGetValue(doubleId, out var e))
                    {
                        var val = e.DisplayTitle ?? doubleId.ToString();
                        return e.IsTag ? $"#{val}" : $"[[{val}]]";
                    }
                    return value;
                }

            case string s:
                {
                    if (long.TryParse(s, out var stringId) && entitiesById.TryGetValue(stringId, out var e))
                    {
                        var val = e.DisplayTitle ?? s;
                        return e.IsTag ? $"#{val}" : $"[[{val}]]";
                    }
                    return value;
                }

            case IList<object?> list:
                for (var i = 0; i < list.Count; i++)
                {
                    list[i] = ResolveEntityRefs(list[i], entitiesById);
                }
                return list;

            case IDictionary<object, object?> dict:
                foreach (var kv in dict)
                {
                    dict[kv.Key] = ResolveEntityRefs(kv.Value, entitiesById);
                }
                return dict;

            default:
                return value;
        }
    }

    public static string ReplaceUuidLinks(string text, Dictionary<string, Entity> entitiesByUUID)
    {
        var re = MyRegex();
        return re.Replace(text, m =>
        {
            var target = m.Groups[1].Value;
            var alias = m.Groups[2].Success ? m.Groups[2].Value : null;

            if (entitiesByUUID.TryGetValue(target, out var targetEntity))
            {
                return alias is null ? $"[[{targetEntity.DisplayTitle}]]" : $"[[{targetEntity.DisplayTitle}|{alias}]]";
            }

            return m.Value;
        });
    }


    [GeneratedRegex(@"\[\[([^\]|]+)(?:\|([^\]]+))?\]\]", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
}
