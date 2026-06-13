namespace LogSeqDBExport;

/// <summary>
/// Represents a database identifier (schema entry) and provides value conversion
/// logic based on the declared type and cardinality.
/// </summary>
public class DBIdent(object? name, bool isBuiltIn, bool isArray, Converter<object?, object?> converter)
{
    private const string PROPERTY_PUBLIC = "~:logseq.property/public?";
    private const string PROPERTY_BUILTIN = "~:logseq.property/built-in?";
    private const string VALUE_TYPE = "~:db/valueType";
    private const string TYPE = "~:logseq.property/type";
    private const string CARDINALITY = "~:db/cardinality";
    private const string DBIDENT = "~:db/ident";
    private const string TITLE = "~:block/title";

    public object? Name { get; } = name;
    public bool IsBuiltIn { get; } = isBuiltIn;
    public bool IsArray { get; } = isArray;
    public Converter<object?, object?> Converter { get; } = converter;

    public static DBIdent FromSourceEntities(IEnumerable<SourceEntity> sourceEntities)
    {
        var name = sourceEntities.FirstOrDefault(se => se.Name == DBIDENT)?.Value;
        var title = sourceEntities.FirstOrDefault(se => se.Name == TITLE)?.Value;
        var isBuiltIn = (bool)(sourceEntities.FirstOrDefault(se => se.Name == PROPERTY_BUILTIN)?.Value ?? false);
        var type = sourceEntities.FirstOrDefault(se => se.Name == TYPE)?.Value;
        var cardinality = sourceEntities.FirstOrDefault(se => se.Name == CARDINALITY)?.Value;

        Converter<object?, object?> converter = type switch
        {
            "~:checkbox" => x => (bool?)x,
            "~:datetime" => x => DateTime.FromFileTime((long?)(double?)x ?? 0),
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

        var isArray = "~:db.cardinality/many".Equals(cardinality);

        return new DBIdent(name, isBuiltIn, isArray, converter);
    }


    internal object? GetValue(IEnumerable<object?> enumerable)
    {
        if (isArray)
        {
            return enumerable.Select(x => Converter(x)).ToArray();
        }

        return Converter(enumerable.First());
    }
}
