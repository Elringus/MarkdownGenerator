using System.IO;
using System.Linq;
using System.Text;

namespace MarkdownWikiGenerator
{
    class Program
    {
        // 0 = dll src path, 1 = dest root
        static void Main(string[] args)
        {
            // put dll & xml on same diretory.
            var target = string.Empty;
            string dest = "md";
            string namespaceMatch = string.Empty;
            if (args.Length == 1)
            {
                target = args[0];
            }
            else if (args.Length == 2)
            {
                target = args[0];
                dest = args[1];
            }
            else if (args.Length == 3) 
            {
                target = args[0];
                dest = args[1];
                namespaceMatch = args[2];
            }

            var types = MarkdownGenerator.Load(target, namespaceMatch);

            var homeBuilder = new MarkdownBuilder();
            homeBuilder.Header(1, "API Reference");
            homeBuilder.AppendLine();

            foreach (var group in types.GroupBy(x => x.Namespace).OrderBy(x => x.Key))
            {
                if (!Directory.Exists(dest)) Directory.CreateDirectory(dest);

                var fileName = group.Key.ToLowerInvariant().Replace(".", "-") + ".md";

                homeBuilder.HeaderWithLink(2, group.Key, fileName);
                homeBuilder.AppendLine();

                var sb = new StringBuilder();
                sb.Append("---\nsidebar: auto\n---\n\n");
                sb.Append($"# {group.Key}\n\n");

                foreach (var item in group.OrderBy(x => x.Name))
                {
                    //homeBuilder.ListLink(MarkdownBuilder.MarkdownCodeQuote(item.BeautifyName), fileName + "#" + item.BeautifyName.Replace("<", "").Replace(">", "").Replace(",", "").Replace(" ", "-").ToLower());
                    sb.Append(item.ToString());
                }

                File.WriteAllText(Path.Combine(dest, fileName), sb.ToString());
                homeBuilder.AppendLine();
            }

            // Generate index.
            File.WriteAllText(Path.Combine(dest, "index.md"), homeBuilder.ToString());
        }
    }
}
