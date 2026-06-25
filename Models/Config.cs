using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace LogSeqDBExport.Models;

/// <summary>
/// Holds configuration settings such as property mappings, tag-to-property
/// mappings and date conversion rules used during parsing and rendering.
/// </summary>
internal class Config
{
    internal class MappingConfig
    {
        public string? Target { get; set; }

        public string? UnmappedTarget { get; set; }

        public string Format { get; set; } = "{0}";

        public Dictionary<string, string> ValueMappings { get; set; } = [];

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ScopeType Scope { get; set; } = ScopeType.Block;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ModeType Mode { get; set; } = ModeType.Property;
    }

    internal enum ScopeType
    {
        Block = 0,
        Page = 1
    }

    internal enum ModeType
    {
        Property = 0,
        Prepend = 1,
        Append = 2
    }

    public double DefaultImageWidth { get; set; } = 250;

    public List<string> Exclusions { get; set; } = [];

    public Dictionary<string, string> PropertyMappings { get; set; } = [];

    private KeyValuePair<Regex, string>? _dateMappingreg = null;

    public KeyValuePair<Regex, string> DateMappingReg => _dateMappingreg ??= KeyValuePair.Create(new Regex(DateMapping[0]), DateMapping[1]);

    public string[]? DateMapping { get; set; }

    public Dictionary<string, MappingConfig> Mappings { get; set; } = [];


    internal static Config ReadFrom(string path)
    {
        return JsonSerializer.Deserialize<Config>(File.ReadAllText(path), new JsonSerializerOptions() { ReadCommentHandling = JsonCommentHandling.Skip }) ?? new Config();
    }

    internal void EnsureDefaultMappings(bool keepAliases)
    {
        // PropertyMappings.Add("~:block/tags", "tags");
        // PropertyMappings.Add("~:block/alias", "aliases");
    }
}
