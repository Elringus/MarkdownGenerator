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
            //args = new [] { @"C:\Users\Elringus\Documents\UnityProjects\Naninovel\DocsGenerator\Assembly-CSharp.dll", @"C:\Users\Elringus\Documents\WebProjects\NaninovelWeb\docs\api", @"^Naninovel.Actions", "..." };

            var target = args[0];
            var dest = args[1];
            var namespaceMatch = args[2];
            var introText = args[3];

            var sb = new StringBuilder();
            sb.Append(introText);

            var types = MarkdownGenerator.Load(target, namespaceMatch)
                .OrderBy(x => x.HasActionTag ? x.ActionTag : x.Name);

            foreach (var type in types)
            {
                if (type.IsAction && type.Type.IsAbstract) continue;
                sb.Append(type.ToString());
            }

            if (!Directory.Exists(dest)) Directory.CreateDirectory(dest);
            File.WriteAllText(Path.Combine(dest, "index.md"), sb.ToString());
        }
    }
}
