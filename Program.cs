using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using System.Text;

namespace MarkdownWikiGenerator
{
    public class Program
    {
        // 0 = dll src path, 1 = dest root, 2 = namespace regex filter, 3 = intro text, 4 = meta output path
        private static void Main(string[] args)
        {
            //args = new [] { @"C:\Users\Elringus\Documents\UnityProjects\Naninovel\DocsGenerator\Assembly-CSharp.dll", @"C:\Users\Elringus\Documents\WebProjects\NaninovelWeb\docs\api", @"^Naninovel.Actions", "..." };

            var target = args[0];
            var dest = args[1];
            var namespaceMatch = args[2];
            var introText = args[3];
            var metaPath = args[4];

            var docBuilder = new StringBuilder();
            docBuilder.Append(introText);

            var metaJArray = new JArray();

            var types = MarkdownGenerator.Load(target, namespaceMatch)
                .OrderBy(x => x.HasActionTag ? x.ActionTag : x.Name);

            foreach (var type in types)
            {
                if (!type.IsAction || type.Type.IsAbstract) continue;
                docBuilder.Append(type.ToString());
                metaJArray.Add(type.ToJson());
            }

            if (!Directory.Exists(dest)) Directory.CreateDirectory(dest);
            File.WriteAllText(Path.Combine(dest, "index.md"), docBuilder.ToString().Replace("*vertical-bar*", "&#124;"), Encoding.UTF8);

            var rootObject = new JObject();
            rootObject["actions"] = metaJArray;
            if (!Directory.Exists(metaPath)) Directory.CreateDirectory(metaPath);
            File.WriteAllText(Path.Combine(metaPath, "metadata.json"), rootObject.ToString(), Encoding.UTF8);
        }
    }
}
