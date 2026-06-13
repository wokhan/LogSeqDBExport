using System.Text.Json.Serialization;

namespace LogSeqDBExport;

/// <summary>
/// Base class to represent an entity extracted from the dump, with its ID, properties (as a lookup to handle multi-valued entries) and its children.
/// </summary>
internal class Entity(double id, Dictionary<string, object?> properties, List<Entity> children) : IComparable
{
    const string IDENT_KEY = "~:db/ident";
    const string IDENT_TAG = "~:logseq.class/Tag";

    internal static readonly string EmptyUUID = Guid.Empty.ToString("N");
    private string? _uuid;
    public string UUID => _uuid ??= Properties.TryGetValue("~:block/uuid", out var v) ? ((string)v!)[2..] : EmptyUUID;

    public string? DisplayTitle => (string?)Properties.GetValueOrDefault("~:block/title", null);

    private bool? _istag;
    public bool IsTag => _istag ??= Properties.TryGetValue("~:block/tags", out var prop) && ((object[])prop).ContainsAny(2d, "#Tag");

    public bool IsRootExportable => Properties.TryGetValue("~:block/tags", out var prop) && ((object?[])prop).ContainsAny("#Page", "#Journal");


    // private bool? _isref;
    // public bool IsReference => _isref ??= Properties["~/block/type"].Any("[[reference]]".Equals);

    public double Id { get; } = id;

    public Dictionary<string, object?> Properties { get; set; } = properties;

    public Dictionary<string, object?> FinalProperties { get; set; } = [];

    //[JsonPropertyName("Properties")]
    //public Dictionary<string, List<object?>> SerializableProperties => Properties.ToDictionary(prop => prop.Key, prop => prop.ToList());

    [JsonIgnore]
    public List<Entity> Children { get; set; } = children;

    [JsonPropertyName("Children")]
    public List<double> ChildrenSerialized => [.. Children.Select(e => e.Id)];

    public int CompareTo(object? obj)
    {
        if (obj is not Entity)
        {
            throw new ArgumentException("Unable to compare, objects are not of the same type.");
        }

        this.Properties.TryGetValue("~:block/order", out var orderA);
        ((Entity)obj).Properties.TryGetValue("~:block/order", out var orderB);

        if (orderA is string oa && orderB is string ob)
            return oa.CompareTo(ob, StringComparison.InvariantCulture);

        if (orderA is string) return -1;

        if (orderB is string) return 1;

        return 0;
    }
}

