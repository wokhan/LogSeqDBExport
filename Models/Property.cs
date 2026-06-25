namespace LogSeqDBExport.Models;

public class Property(PropertyType Type, bool Internal, object? Value)
{
    internal static Property FromSourceEntries(IGrouping<string, SourceEntry> it, Dictionary<string, PropertyType> schema)
    {
        var type = schema.GetValueOrDefault(it.Key, schema["_default"]);
        var val = type.GetValue(it);

        return new Property(type, false, val);
    }
}