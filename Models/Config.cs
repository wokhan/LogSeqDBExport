using System.Text.Json;
using System.Text.RegularExpressions;

namespace LogSeqDBExport.Models;

/// <summary>
/// Holds configuration settings such as property mappings, tag-to-property
/// mappings and date conversion rules used during parsing and rendering.
/// </summary>
internal class Config
{
    public double DefaultImageWidth { get; set; } = 250;
    public List<string> Exclusions { get; set; } = [];
    public Dictionary<string, string[]> TagsToPropertyMappings { get; set; } = [];
    public Dictionary<string, string> PageOnlyPropertyMappings { get; set; } = [];
    public Dictionary<string, string> PropertyMappings { get; set; } = [];

    private Dictionary<string, string>? _pagePropertyMappingsCache;
    public Dictionary<string, string> PagePropertyMappings => _pagePropertyMappingsCache ??= PageOnlyPropertyMappings.Concat(PropertyMappings).ToDictionary(kv => kv.Key, kv => kv.Value);

    private KeyValuePair<Regex, string>? _dateMappingreg = null;
    public KeyValuePair<Regex, string> DateMappingReg => _dateMappingreg ??= KeyValuePair.Create(new Regex(DateMapping[0]), DateMapping[1]);
    public string[]? DateMapping { get; set; }

    internal static Config ReadFrom(string path)
    {
        return JsonSerializer.Deserialize<Config>(File.ReadAllText(path)) ?? new Config();
    }

    internal void EnsureDefaultMappings(bool keepAliases)
    {
        PropertyMappings.Add("~:block/tags", "tags");
        PropertyMappings.Add("~:block/alias", "aliases");
    }
}
