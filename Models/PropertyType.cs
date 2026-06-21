namespace LogSeqDBExport.Models;

/// <summary>
/// Represents a database identifier (schema entry) and provides value conversion
/// logic based on the declared type and cardinality.
/// </summary>
public record PropertyType(object? Name, bool IsBuiltIn, bool IsArray, Converter<object?, object?> Converter, bool IsRefType)
{
    internal const string DBIDENT = "~:db/ident";
    private const string PROPERTY_PUBLIC = "~:logseq.property/public?";
    private const string PROPERTY_BUILTIN = "~:logseq.property/built-in?";
    private const string VALUE_TYPE = "~:db/valueType";
    private const string TYPE = "~:logseq.property/type";
    private const string CARDINALITY = "~:db/cardinality";
    private const string TITLE = "~:block/title";
    private const string VALUE_REF = "~:db.type/ref";
    private const string CARDINALITY_MANY = "~:db.cardinality/many";

    public static PropertyType FromSourceEntries(IEnumerable<SourceEntry> sourceEntities)
    {
        var name = sourceEntities.FirstOrDefault(se => se.Name == DBIDENT)?.Value;
        var title = sourceEntities.FirstOrDefault(se => se.Name == TITLE)?.Value;
        var isBuiltIn = (bool)(sourceEntities.FirstOrDefault(se => se.Name == PROPERTY_BUILTIN)?.Value ?? false);
        var type = sourceEntities.FirstOrDefault(se => se.Name == TYPE)?.Value;
        var cardinality = sourceEntities.FirstOrDefault(se => se.Name == CARDINALITY)?.Value;
        var isRefType = VALUE_REF.Equals(sourceEntities.FirstOrDefault(se => se.Name == VALUE_TYPE)?.Value);
        var isArray = CARDINALITY_MANY.Equals(cardinality);
        
        Converter<object?, object?> converter = type switch
        {
            "~:checkbox" => x => (bool?)x,
            "~:datetime" => x => DateTimeOffset.FromUnixTimeMilliseconds((long?)(double?)x ?? 0).DateTime,
            "~:raw-number" => x => (double?)x,
            "~:map" => x => ((List<object>)x!).Skip(1).Chunk(2).ToDictionary(x => x[0], x => x[1]),
            "~:number" => x => (double?)x,
            "~:coll" => x => (IList<object>?)x ?? [],

            // "~:keyword" => x => x,
            // "~:entity" =>  x => x,
            // "~:any" =>  x => x,
            // "~:class" =>  x => x,
            // "~:property" =>  x => x,
            // "~:page" =>  x => x,
            //  "~:node" =>
            
            _ => x => x
        };

        return new PropertyType(name, isBuiltIn, isArray, converter, isRefType);
    }


    internal object? GetValue(IEnumerable<object?> enumerable)
    {
        if (IsArray)
        {
            return enumerable.Select(x => Converter(x)).ToArray();
        }

        return Converter(enumerable.First());
    }
}
