namespace LogSeqDBExport.Models;

public class Property(PropertyType Type, object? Value)
{
    internal static Property FromSourceEntries(IGrouping<string, SourceEntry> it, Dictionary<string, PropertyType> schema)
    {
        var type = schema.GetValueOrDefault(it.Key, schema["_default"]);
        var val = type.GetValue(it.Select(a => a.Value));

        return new Property(type, val);
    }
}