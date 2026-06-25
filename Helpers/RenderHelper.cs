using System.Text;
using LogSeqDBExport.Models;
using YamlDotNet.Serialization;

namespace LogSeqDBExport.Helpers;

/// <summary>
/// Renders pages and YAML front-matter from `Entity` instances into Markdown files.
/// </summary>
internal static class RenderHelper
{
    static readonly ISerializer serializer = new SerializerBuilder().Build();

    private static void RenderFrontMatterHeader(Dictionary<string, object?> properties, StringBuilder sb)
    {
        sb.AppendLine("---");
        var yaml = serializer.Serialize(properties);
        sb.Append(yaml);
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


    public static void RenderPages(Options opt, IEnumerable<Entity> entities, Config config)
    {
        foreach (var entity in entities)
        {
            if (!entity.IsRootExportable
                || (opt.ExcludeDeleted && entity.IsDeleted)
                || (entity.Contents is not null && config.Exclusions.Contains(entity.Contents))
                || (opt.ResolveAliases && entity.AliasOf is not null))
            {
                continue;
            }

            RenderPage(opt, config, entity);
        }
    }

    private static void RenderPage(Options options, Config config, Entity rootEntity)
    {
        try
        {
            var basepath = options.OutDir;
            if (options.UsePropertyForFolder is not null && rootEntity.Properties.GetValueOrDefault(options.UsePropertyForFolder, "_other") is string folder)
            {
                basepath = Path.Combine(basepath, folder);
            }
            Directory.CreateDirectory(basepath);

            var path = Path.Combine(basepath, $"{rootEntity.Contents}.md");

            var sb = new StringBuilder(5000);

            RenderFrontMatterHeader(rootEntity.Properties, sb);

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
        var filtered = !options.ExportOnlyPageChildren ? entity.Children : entity.Children.Where(c => rootPageId.Equals(c.PageId));
        var islist = false;
        var counter = 0;
        foreach (var child in filtered)
        {
            if (child.RawProperties.ContainsKey("~:logseq.property/order-list-type"))
            {
                if (!islist) counter = 0;
                counter++;
                islist = true;
            }
            else
            {
                islist = false;
            }

            sb.Append(new String('\t', indentLevel)).Append(islist ? $"{counter}. " : "- ");

            if (child.IsPage)
            {
                sb.AppendLine($"[[{child.Contents}]]");

                if (!child.IsDeleted && child.Contents is not null && !config.Exclusions.Contains(child.Contents))
                {
                    RenderPage(options, config, child);
                }
                continue;
            }

            sb.Append(child.Contents);

            var tags = (object[]?)child.Properties.GetValueOrDefault("tags", null);
            if (tags is not null)
            {
                sb.Append(' ').Append(String.Join(" ", tags.Select(tag => $"#{tag}")));
            }

            sb.AppendLine();

            RenderProperties(child.Properties, sb, indentLevel);

            if (child.AssetType is not null)
            {
                sb.Append(new String('\t', indentLevel + 1))
                  .AppendLine($"- ![[assets/{child.UUID}.{child.AssetType}|{config.DefaultImageWidth}]]");
            }


            RenderChildren(rootPageId, child, sb, config, options, indentLevel + 1);
        }
    }
}
