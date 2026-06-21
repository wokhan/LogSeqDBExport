using System.Text.RegularExpressions;
using LogSeqDBExport.Models;

namespace LogSeqDBExport.Helpers;

internal static partial class EntitiesManager
{

    internal static Dictionary<double, Entity> FromSourceEntries(List<SourceEntry> sourceEntries, out Dictionary<string, PropertyType> schema)
    {
        var localschema = sourceEntries.GroupBy(e => e.Id)
                                       .Where(e => e.Any(ev => ev.Name == PropertyType.DBIDENT))
                                       .ToDictionary(g => (string)g.First(ev => ev.Name == PropertyType.DBIDENT)!.Value!, PropertyType.FromSourceEntries);

        localschema.Add("_default", new PropertyType("", true, false, x => x, false));

        schema = localschema;

        var entitiesById = sourceEntries.GroupBy(e => e.Id)
                                        .ToDictionary(g => g.Key, g => CreateEntity(g, localschema));

        SetAliases(entitiesById);

        ResolveParents(entitiesById);

        SortEntities(entitiesById.Values);

        return entitiesById;
    }

    private static void SetAliases(Dictionary<double, Entity> entitiesById)
    {
        var x = entitiesById.Values.Where(e => e.RawProperties.ContainsKey("~:block/alias"))
                                   .SelectMany(e => e.RawProperties.TryGetValue("~:block/alias", out var aliases) && aliases is object[] aliasesArray ? aliasesArray.Select(alias => (aliasId: (double)alias, e)) : []);
        foreach (var (aliasId, targetEntity) in x)
        {
            entitiesById[aliasId].AliasOf = targetEntity;
        }
    }

    private static Entity CreateEntity(IGrouping<double, SourceEntry> g, Dictionary<string, PropertyType> schema)
    {
        var properties = g.GroupBy(it => it.Name)
                          .ToDictionary(it => it.Key, it => schema.GetValueOrDefault(it.Key, schema["_default"]).GetValue(it.Select(a => a.Value)));

        var realprops = g.GroupBy(it => it.Name).ToDictionary(it => it.Key, it => Property.FromSourceEntries(it, schema));

        return new Entity(g.Key, properties, realprops, []);
    }


    internal static void ResolveAndMapProperties(Dictionary<double, Entity> entitiesById, Dictionary<string, PropertyType> schema, Options options, Config config)
    {
        var entitiesByUUID = entitiesById.Values.Where(e => e.UUID != Entity.EmptyUUID)
                                                .ToDictionary(e => e.UUID, e => e);

        foreach (var (id, entity) in entitiesById)
        {
            ResolveAndMapProperties(entitiesById, schema, options, config, entitiesByUUID, entity);
        }
    }

    private static void ResolveAndMapProperties(Dictionary<double, Entity> entitiesById, Dictionary<string, PropertyType> schema, Options options, Config config, Dictionary<string, Entity> entitiesByUUID, Entity entity)
    {
        if (entity.RawProperties.TryGetValue("~:block/title", out var title) && title is string titleStr)
        {
            titleStr = ReplaceUuidLinks(titleStr, entitiesByUUID, options);
            titleStr = FormatDateRef(titleStr, config.DateMappingReg.Key, config.DateMappingReg.Value);

            entity.Contents = titleStr;
        }

        var propertyMappings = entity.IsPage ? config.PagePropertyMappings : config.PropertyMappings;
        var mappedProperties = entity.RawProperties.Where(p => propertyMappings.ContainsKey(p.Key))
                                                   .Select(e => (SourceKey: e.Key, TargetKey: propertyMappings[e.Key], Value: e.Value));

        foreach (var (SourceKey, TargetKey, Value) in mappedProperties)
        {
            object? targetValue = Value;

            //Embed type in property directly? Like property.Type.IsRefType ?
            if (schema.TryGetValue(SourceKey, out var schemaEntry) && schemaEntry.IsRefType)
            {
                var needsAliasResolve = options.ResolveAliases && SourceKey != "~:block/alias";
                targetValue = ResolveEntityRefs(targetValue, entitiesById, needsAliasResolve);
            }

            entity.Properties[TargetKey] = targetValue;

            if (TargetKey == "tags" && targetValue is object[] tags)
            {
                foreach (var mapping in config.TagsToPropertyMappings)
                {
                    if (tags.Contains(mapping.Key))
                    {
                        entity.Properties[mapping.Value[0]] = mapping.Value[1];
                    }
                }
            }

        }
    }

    private static string FormatDateRef(string input, Regex key, string targetFormat)
    {
        return key.Replace(input, targetFormat);
    }

    internal static void ResolveParents(Dictionary<double, Entity> entitiesById)
    {
        foreach (var entity in entitiesById.Values)
        {
            if (entity.RawProperties.TryGetValue("~:block/parent", out var parent) && parent is double parentId && entitiesById.TryGetValue(parentId, out var parentEntity))
            {
                parentEntity.Children.Add(entity);
            }
        }
    }

    internal static void SortEntities(IEnumerable<Entity> entities)
    {
        foreach (var entity in entities)
        {
            entity.Children.Sort();

            SortEntities(entity.Children);
        }
    }

    public static object? ResolveEntityRefs(object? value, Dictionary<double, Entity> entitiesById, bool resolveAlias)
    {
        if (value is double || value is long)
        {
            var dval = (double)value;

            if (!entitiesById.TryGetValue(dval, out var target))
            {
                return value;
            }

            if (resolveAlias)
            {
                target = ResolveAliasRecursive(target);
            }

            if (target is not null)
            {
                if (target.IsDeleted)
                {
                    Console.WriteLine($"Value '{value}' resolved to a deleted entity.");
                }
                var val = target.Contents ?? value.ToString();
                return target.IsPage ? $"[[{val}]]" : val;
            }
        }
        else if (value is object?[] array)
        {
            return array.Select(item => ResolveEntityRefs(item, entitiesById, resolveAlias)).ToArray();
        }

        Console.WriteLine($"Unable to resolve ref for value '{value}' (type: {value?.GetType()})");
        return value;
    }

    private static Entity ResolveAliasRecursive(Entity targetEntity)
    {
        while (targetEntity.AliasOf is not null)
        {
            Console.WriteLine($"Alias '{targetEntity.Contents}' has been mapped to '{targetEntity.AliasOf.Contents}'.");
            targetEntity = targetEntity.AliasOf;
        }

        return targetEntity;
    }

    public static string ReplaceUuidLinks(string text, Dictionary<string, Entity> entitiesByUUID, Options options)
    {
        var re = MyRegex();
        return re.Replace(text, m =>
        {
            var target = m.Groups[1].Value;
            var alias = m.Groups[2].Success ? m.Groups[2].Value : null;

            if (entitiesByUUID.TryGetValue(target, out var targetEntity))
            {
                if (options.ResolveAliases)
                {
                    targetEntity = ResolveAliasRecursive(targetEntity);
                }
                return alias is null ? $"[[{targetEntity.Contents}]]" : $"[[{targetEntity.Contents}|{alias}]]";
            }

            return m.Value;
        });
    }

    [GeneratedRegex(@"\[\[([^\]|]+)(?:\|([^\]]+))?\]\]", RegexOptions.Compiled)]
    private static partial Regex MyRegex();

}