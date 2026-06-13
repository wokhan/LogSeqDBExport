using System.Text;
using System.Text.Json;

namespace LogSeqDBExport.Helpers;

/// <summary>
/// Renders pages and YAML front-matter from `Entity` instances into Markdown files.
/// </summary>
internal static class RenderHelper
{
    private static void RenderFrontMatterHeader(Dictionary<string, object?> properties, StringBuilder sb, Dictionary<double, Entity> entitiesById, Config config)
    {
        sb.AppendLine("---");
        RenderProperties(properties, sb);
        sb.AppendLine("---\n");
    }


    private static void RenderProperties(Dictionary<string, object?> properties, StringBuilder sb, bool inline = false, int indentLevel = 0)
    {
        foreach (var prop in properties)
        {
            sb.Append(new String('\t', indentLevel));

            string val = prop.Value switch
            {
                Array valuesArray => JsonSerializer.Serialize(valuesArray),
                null => "null",
                _ => $"\"{prop.Value}\""
            };

            if (inline)
            {
                sb.AppendLine($"  [{prop.Key}:: {val}]");
            }
            else
            {

                sb.AppendLine($"{prop.Key}: {val}");
            }
        }
    }


    public static void RenderPages(Options opt, Dictionary<double, Entity> entitiesById, Config config)
    {
        foreach (var rootEntity in entitiesById.Values.Where(e => e.IsRootExportable && !string.IsNullOrWhiteSpace(e.DisplayTitle)))
        {
            try
            {
                var basepath = opt.OutDir;
                if (opt.UseTypeForFolder && rootEntity.FinalProperties.GetValueOrDefault("type", "_other") is string type)
                {
                    basepath = Path.Combine(basepath, type);
                }
                Directory.CreateDirectory(basepath);

                var path = Path.Combine(basepath, $"{rootEntity.DisplayTitle}.md");

                var sb = new StringBuilder(5000);

                RenderFrontMatterHeader(rootEntity.FinalProperties, sb, entitiesById, config);

                RenderChildren(rootEntity.Id, rootEntity, sb, config, opt.ExportOnlyPageChildren, 0);

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing entity {rootEntity.Id}: {ex.Message}");
            }
        }
    }

    private static void RenderChildren(double rootPageId, Entity entity, StringBuilder sb, Config config, bool checkPage = true, int indentLevel = 0)
    {
        var filtered = !checkPage ? entity.Children : entity.Children.Where(c => c.Properties["~:block/page"] is double pageId && pageId == rootPageId);
        foreach (var child in filtered)
        {
            sb.Append(new String('\t', indentLevel)).Append("- ").Append(child.DisplayTitle);

            if (child.Properties.ContainsKey("~:block/tags"))
            {
                sb.Append(' ').Append(String.Join(" ", child.Properties["~:block/tags"]));
            }

            sb.AppendLine();

            RenderProperties(child.FinalProperties, sb, true, indentLevel);

            RenderChildren(rootPageId, child, sb, config, checkPage, indentLevel + 1);
        }
    }
}
