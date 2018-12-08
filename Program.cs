using System.IO;
using System.Linq;
using System.Text;

namespace MarkdownWikiGenerator
{
    public class Program
    {
        // 0 = dll src path, 1 = dest root, 2 = namespace regex filter
        private static void Main(string[] args)
        {
            //args = new [] { @"C:\Users\Elringus\AppData\Local\Temp\NaninovelDocsGenerator\Assembly-CSharp.dll", @"C:\Users\Elringus\Documents\WebProjects\NaninovelWeb\docs\api", @"^Naninovel" };

            var target = string.Empty;
            var dest = "md";
            var namespaceMatch = string.Empty;
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
            foreach (var group in types.GroupBy(x => x.Namespace).OrderBy(x => x.Key))
            {
                if (!Directory.Exists(dest)) Directory.CreateDirectory(dest);

                var fileName = group.Key.ToLowerInvariant().Replace(".", "-") + ".md";

                var sb = new StringBuilder();
                sb.Append("---\nsidebar: auto\n---\n\n");
                sb.Append($"# {group.Key}\n\n");

                foreach (var type in group.OrderBy(x => x.HasActionTag ? x.ActionTag : x.Name))
                {
                    if (type.IsAction && type.Type.IsAbstract) continue;
                    sb.Append(type.ToString());
                }

                File.WriteAllText(Path.Combine(dest, fileName), sb.ToString());
            }
        }
    }
}
