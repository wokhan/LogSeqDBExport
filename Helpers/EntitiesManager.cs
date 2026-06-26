using System.Text.RegularExpressions;
using LogSeqDBExport.Models;

namespace LogSeqDBExport.Helpers;

internal static partial class EntitiesManager
{
    private static double PageTagId = 0;

    internal static Dictionary<double, Entity> FromSourceEntries(List<SourceEntry> sourceEntries, out Dictionary<string, PropertyType> schema)
    {
        var localschema = sourceEntries.GroupBy(e => e.Id)
                                       .Where(e => e.Any(ev => ev.Name == PropertyType.DBIDENT))
                                       .ToDictionary(g => (string)g.First(ev => ev.Name == PropertyType.DBIDENT)!.Value!, PropertyType.FromSourceEntries);

        localschema.Add("_default", new PropertyType(0, "", true, false, x => x, false));

        PageTagId = localschema["~:logseq.class/Page"].Id;

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
        var aliasMappings = entitiesById.Values.Where(e => e.RawProperties.ContainsKey("~:block/alias"))
                                               .SelectMany(e => e.RawProperties.TryGetValue("~:block/alias", out var aliases) && aliases is object[] aliasesArray ? aliasesArray.Select(alias => (aliasId: (double)alias, e)) : []);
        foreach (var (aliasId, targetEntity) in aliasMappings)
        {
            entitiesById[aliasId].AliasOf = targetEntity;
        }
    }

    private static Entity CreateEntity(IGrouping<double, SourceEntry> sourceEntriesById, Dictionary<string, PropertyType> schema)
    {
        var properties = sourceEntriesById.GroupBy(it => it.Name)
                                          .ToDictionary(it => it.Key, it => schema.GetValueOrDefault(it.Key, schema["_default"]).GetValue(it));

        return new Entity(sourceEntriesById.Key, properties);
    }


    internal static void ResolveAndMapProperties(Dictionary<double, Entity> entitiesById, Dictionary<string, PropertyType> schema, Options options, Config config)
    {
        var entitiesByUUID = entitiesById.Values.Where(e => e.UUID != Entity.EmptyUUID)
                                                .ToDictionary(e => e.UUID, e => e);

        foreach (var entity in entitiesById.Values)
        {
            ResolveAndMapProperties(entity, schema, options, config, entitiesById, entitiesByUUID);
        }
    }

    private static void ResolveAndMapProperties(Entity entity, Dictionary<string, PropertyType> schema, Options options, Config config, Dictionary<double, Entity> entitiesById, Dictionary<string, Entity> entitiesByUUID)
    {
        if (entity.RawProperties.TryGetValue("~:block/title", out var title) && title is string titleStr)
        {
            titleStr = ReplaceUuidLinks(titleStr, entitiesByUUID, options);
            titleStr = FormatDateRef(titleStr, config.DateMappingReg.Key, config.DateMappingReg.Value);
            titleStr = FixTilde(titleStr);

            var cnt = 0;
            if (IsPage(entity) && ((cnt = entitiesById.Values.Count(e => e.Contents == titleStr)) > 1))
            {
                entity.Contents = $"{titleStr}_{cnt - 1}";
            }
            else
            {
                entity.Contents = titleStr;
            }
        }

        foreach (var property in entity.RawProperties)
        {
            object? targetValue = property.Value;

            // Embed type in property directly? Like property.Type.IsRefType ?
            if (schema.TryGetValue(property.Key, out var schemaEntry) && schemaEntry.IsRefType)
            {
                var needsAliasResolve = options.ResolveAliases && property.Key != "~:block/alias";
                targetValue = ResolveEntityRefs(targetValue, entitiesById, needsAliasResolve);
            }

            if (config.Mappings.TryGetValue(property.Key, out var mapping))
            {
                if (!IsPage(entity) && mapping.Scope == Config.ScopeType.Page)
                {
                    continue;
                }

                if (mapping.ValueMappings.Count > 0)
                {
                    foreach (var map in mapping.ValueMappings)
                    {
                        if (((targetValue as object[])?.Contains(map.Key) ?? false) || map.Key.Equals(targetValue))
                        {
                            SetMappedProperty(entity, mapping, mapping.Target, map.Value);
                        }
                    }

                    //TODO: exclude mapped values (or add option to consider if required or not)
                    if (mapping.UnmappedTarget is not null)
                    {
                        SetMappedProperty(entity, mapping, mapping.UnmappedTarget, targetValue);
                    }
                }
                else if (mapping.Mode != Config.ModeType.Property || mapping.Target is not null)
                {
                    SetMappedProperty(entity, mapping, mapping.Target, targetValue);
                }
            }
        }
    }

    private static void SetMappedProperty(Entity entity, Config.MappingConfig mapping, string? targetPropertyKey, object? targetValue)
    {
        var formattedValue = (object?)(targetValue as object[])?.Select(v => String.Format(mapping.Format, v)).ToArray() ?? String.Format(mapping.Format, targetValue);

        switch (mapping.Mode)
        {
            case Config.ModeType.Property:
                if (targetPropertyKey is null)
                {
                    throw new NullReferenceException($"A target property key must be specified when using the Property mode.");
                }
                entity.Properties[targetPropertyKey] = formattedValue;
                break;

            case Config.ModeType.Append:
                entity.Contents += " " + formattedValue;
                break;

            case Config.ModeType.Prepend:
                entity.Contents = $"{formattedValue} {entity.Contents}";
                break;
        }
    }

    private static bool IsPage(Entity entity)
    {
        return entity.RawProperties.TryGetValue("~:block/tags", out var tags) && tags is object[] tagsArray && tagsArray.Contains(PageTagId);
    }

    private static string FixTilde(string titleStr)
    {
        if (titleStr.StartsWith("~~~"))
        {
            return titleStr[1..];
        }

        return titleStr;
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