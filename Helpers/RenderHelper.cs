using System.Globalization;
using System.Text;
using System.Text.Json;
using YamlDotNet.Serialization;

namespace LogSeqDBExport.Helpers;

/// <summary>
/// Renders pages and YAML front-matter from `Entity` instances into Markdown files.
/// </summary>
internal static class RenderHelper
{
    private static void RenderFrontMatterHeader(Dictionary<string, object?> properties, StringBuilder sb)
    {
        sb.AppendLine("---");
        var serializer = new SerializerBuilder().Build();
        var yaml = serializer.Serialize(properties);
        sb.AppendLine(yaml);
        sb.AppendLine("---\n");
    }

    private static void RenderProperties(Dictionary<string, object?> properties, StringBuilder sb, int indentLevel = 0)
    {
        foreach (var prop in properties.Where(p => p.Key != "tags"))
        {
            var val = prop.Value is object[] valuesArray ? String.Join(", ", valuesArray) : $"{prop.Value}";
            sb.Append(new String('\t', indentLevel))
              .AppendLine($"  [{prop.Key}:: {val}]");
        }
    }


    public static void RenderPages(Options opt, Dictionary<double, Entity> entitiesById, Config config)
    {
        foreach (var rootEntity in entitiesById.Values.Where(e => e.IsRootExportable && !string.IsNullOrWhiteSpace(e.DisplayTitle)))
        {
            RenderPage(opt, config, rootEntity);
        }
    }

    private static void RenderPage(Options options, Config config, Entity rootEntity)
    {
        try
        {
            var basepath = options.OutDir;
            if (options.UseTypeForFolder && rootEntity.FinalProperties.GetValueOrDefault("type", "_other") is string type)
            {
                basepath = Path.Combine(basepath, type);
            }
            Directory.CreateDirectory(basepath);

            var path = Path.Combine(basepath, $"{rootEntity.DisplayTitle}.md");

            var sb = new StringBuilder(5000);

            RenderFrontMatterHeader(rootEntity.FinalProperties, sb);

            RenderChildren(rootEntity.Id, rootEntity, sb, config, options, 0);

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing entity {rootEntity.Id}: {ex.Message}");
        }
    }

    private static void RenderChildren(double rootPageId, Entity entity, StringBuilder sb, Config config, Options options, int indentLevel = 0)
    {
        var filtered = !options.ExportOnlyPageChildren ? entity.Children : entity.Children.Where(c => c.Properties["~:block/page"] is double pageId && pageId == rootPageId);
        foreach (var child in filtered)
        {
            sb.Append(new String('\t', indentLevel)).Append("- ");

            if (child.IsPage)
            {
                sb.AppendLine($"[[{child.DisplayTitle}]]");

                RenderPage(options, config, child);
                continue;
            }

            sb.Append(child.DisplayTitle);

            if (child.FinalProperties.TryGetValue("tags", out object? value) && value is object[] tags)
            {
                sb.Append(' ').Append(String.Join(" ", tags));
            }

            sb.AppendLine();

            RenderProperties(child.FinalProperties, sb, indentLevel);

            RenderChildren(rootPageId, child, sb, config, options, indentLevel + 1);
        }
    }
}
