using System.Security.Cryptography.X509Certificates;
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

    public string? DisplayTitle => Properties.TryGetValue("~:block/title", out var title) && !string.IsNullOrWhiteSpace((string?)title) ? (string?)title : UUID;

    private bool? _istag;
    public bool IsTag => _istag ??= Properties.TryGetValue("~:block/tags", out var prop) && prop is object[] propArray && propArray.ContainsAny(2d, "#Tag");

    public bool IsRootExportable => Properties.TryGetValue("~:block/tags", out var prop) && prop is object[] propArray && propArray.ContainsAny("#Page", "#Journal");

    public double Id { get; } = id;

    public Dictionary<string, object?> Properties { get; set; } = properties;

    public Dictionary<string, object?> FinalProperties { get; set; } = [];

    [JsonIgnore]
    public List<Entity> Children { get; set; } = children;

    [JsonPropertyName("Children")]
    public List<double> ChildrenSerialized => [.. Children.Select(e => e.Id)];

    public bool IsPage => FinalProperties.TryGetValue("tags", out var tags) && tags is object[] tagsArray && tagsArray.Contains("#Page");

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

