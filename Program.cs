using System.IO;
using System.Linq;
using System.Text;

namespace MarkdownWikiGenerator
{
    public class Program
    {
        // 0 = dll src path, 1 = dest root, 2 = namespace regex filter, 3 = intro text
        private static void Main(string[] args)
        {
            //args = new [] { @"C:\Users\Elringus\AppData\Local\Temp\NaninovelDocsGenerator\Assembly-CSharp.dll", @"C:\Users\Elringus\Documents\WebProjects\NaninovelWeb\docs\api", @"^Naninovel", "..." };

            var target = args[0];
            var dest = args[1];
            var namespaceMatch = args[2];
            var introText = args[3];

            var types = MarkdownGenerator.Load(target, namespaceMatch);
            foreach (var group in types.GroupBy(x => x.Namespace).OrderBy(x => x.Key))
            {
                if (!Directory.Exists(dest)) Directory.CreateDirectory(dest);

                //var fileName = group.Key.ToLowerInvariant().Replace(".", "-") + ".md";
                var fileName = "index.md";

                var sb = new StringBuilder();
                sb.Append(introText);

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
