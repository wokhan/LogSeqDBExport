using System.Text.Json.Serialization;
using YamlDotNet.Core.Tokens;

namespace LogSeqDBExport.Models;

/// <summary>
/// Base class to represent an entity extracted from the dump, with its ID, properties (as a lookup to handle multi-valued entries) and its children.
/// </summary>
internal class Entity(double id, Dictionary<string, object?> rawProperties, Dictionary<string, Property> realprops, List<Entity> children) : IComparable
{
    private const string ASSET_TYPE = "~:logseq.property.asset/type";
    private const string ASSET_WIDTH = "~:logseq.property.asset/width";
    private const string PROPERTY_DELETED_AT = "~:logseq.property/deleted-at";
    private const string BLOCK_TAG = "~:block/tags";
    private const string BLOCK_UUID = "~:block/uuid";
    private const string BLOCK_TITLE = "~:block/title";
    private const string BLOCK_ORDER = "~:block/order";
    internal static readonly string EmptyUUID = Guid.Empty.ToString("N");

    public double Id { get; } = id;

    private string? _uuid;
    public string UUID => _uuid ??= RawProperties.TryGetValue(BLOCK_UUID, out var v) ? ((string)v!)[2..] : EmptyUUID;

    private string? _contents = null;
    public string? Contents
    {
        get => _contents is null && RawProperties.TryGetValue(BLOCK_TITLE, out var title) && title is string titleStr ? titleStr : _contents;
        set => _contents = value;
    }

    public string? AssetType => (string?)RawProperties.GetValueOrDefault(ASSET_TYPE, null);

    public double? AssetWidth => (double?)RawProperties.GetValueOrDefault(ASSET_WIDTH, null);

    private bool? _istag;

    public bool IsDeleted => RawProperties.ContainsKey(PROPERTY_DELETED_AT);

    public bool IsRootExportable => Properties.TryGetValue("tags", out var prop) && prop is object[] propArray && propArray.ContainsAny("Page", "Journal");

    public bool IsTag => _istag ??= Properties.TryGetValue("tags", out var prop) && prop is object[] propArray && propArray.Contains("Tag");

    public bool IsPage => Properties.TryGetValue("tags", out var tags) && tags is object[] tagsArray && tagsArray.Contains("Page");

    [JsonIgnore]
    public Entity? AliasOf { get; set; }

    [JsonPropertyName("AliasOf")]
    public double? AliasOfId => AliasOf?.Id;

    public Dictionary<string, object?> RawProperties { get; set; } = rawProperties;

    public Dictionary<string, object?> Properties { get; set; } = [];

    public Dictionary<string, Property> RealProperties { get; set; } = realprops;

    [JsonIgnore]
    public List<Entity> Children { get; set; } = children;

    [JsonPropertyName("Children")]
    public List<double> ChildrenSerialized => [.. Children.Select(e => e.Id)];

    public int CompareTo(object? obj)
    {
        if (obj is not Entity)
        {
            throw new ArgumentException($"Cannot compare '{obj?.GetType()}' with '{typeof(Entity)}'");
        }

        this.RawProperties.TryGetValue(BLOCK_ORDER, out var orderA);
        ((Entity)obj).RawProperties.TryGetValue(BLOCK_ORDER, out var orderB);

        if (orderA is string oa && orderB is string ob)
            return oa.CompareTo(ob, StringComparison.InvariantCulture);

        if (orderA is string) return -1;

        if (orderB is string) return 1;

        return 0;
    }
}
