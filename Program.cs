using Newtonsoft.Json.Linq;
using System;
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
            try
            {
                //args = new [] { @"C:\Users\Elringus\Documents\UnityProjects\Naninovel\DocsGenerator\Assembly-CSharp.dll", @"C:\Users\Elringus\Documents\WebProjects\NaninovelWeb\docs\api", @"^Naninovel.Actions", "..." };

                var target = args.ElementAtOrDefault(0);
                var dest = args.ElementAtOrDefault(1);
                var namespaceMatch = args.ElementAtOrDefault(2);
                var introText = args.ElementAtOrDefault(3);
                var metaPath = args.ElementAtOrDefault(4);

                var docBuilder = new StringBuilder();
                docBuilder.Append(introText);

                var metaJArray = new JArray();

                var types = MarkdownGenerator.Load(target, namespaceMatch)
                    .OrderBy(x => x.HasActionAlias ? x.ActionAlias : x.Name);

                foreach (var type in types)
                {
                    if (!type.IsAction || type.Type.IsAbstract) continue;
                    docBuilder.Append(type.ToString());
                    metaJArray.Add(type.ToJson());
                }

                if (!Directory.Exists(dest)) Directory.CreateDirectory(dest);
                File.WriteAllText(Path.Combine(dest, "index.md"), docBuilder.ToString().Replace("*vertical-bar*", "&#124;"), Encoding.UTF8);

                if (string.IsNullOrWhiteSpace(metaPath)) return;
                var rootObject = new JObject();
                rootObject["actions"] = metaJArray;
                if (!Directory.Exists(metaPath)) Directory.CreateDirectory(metaPath);
                File.WriteAllText(Path.Combine(metaPath, "metadata.json"), rootObject.ToString(), Encoding.UTF8);
            }
            catch (Exception e) { Console.WriteLine("Error: " + e.Message); Console.ReadKey(); }
        }
    }
}
