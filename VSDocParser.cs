using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MarkdownWikiGenerator
{
    public enum MemberType
    {
        Field = 'F',
        Property = 'P',
        Type = 'T',
        Event = 'E',
        Method = 'M',
        None = 0
    }

    public class XmlDocumentComment
    {
        public MemberType MemberType { get; set; }
        public string ClassName { get; set; }
        public string MemberName { get; set; }
        public string Summary { get; set; }
        public string Remarks { get; set; }
        public string Example { get; set; }
        public Dictionary<string, string> Parameters { get; set; }
        public string Returns { get; set; }

        public override string ToString()
        {
            return MemberType + ":" + ClassName + "." + MemberName;
        }
    }

    public static class VSDocParser
    {
        public static XmlDocumentComment[] ParseXmlComment(XDocument xDocument) {
            return ParseXmlComment(xDocument, null);
        }

        internal static XmlDocumentComment[] ParseXmlComment(XDocument xDocument, string namespaceMatch) {
            return xDocument.Descendants("member")
                .Select(x => {
                    var match = Regex.Match(x.Attribute("name").Value, @"(.):(.+)\.([^.()]+)?(\(.+\)|$)");
                    if (!match.Groups[1].Success) return null;

                    var memberType = (MemberType)match.Groups[1].Value[0];
                    if (memberType == MemberType.None) return null;

                    // Summary body.
                    var summaryXml = x.Elements("summary").FirstOrDefault()?.ToString()
                        ?? x.Element("summary")?.ToString()
                        ?? "";
                    summaryXml = Regex.Replace(summaryXml, @"<\/?summary>", string.Empty);
                    summaryXml = Regex.Replace(summaryXml, @"<para\s*/>", Environment.NewLine);
                    summaryXml = Regex.Replace(summaryXml, @"<see cref=""\w:([^\""]*)""\s*\/>", m => ResolveSeeElement(m, namespaceMatch));
                    summaryXml = Regex.Replace(summaryXml, @"<see href=""(.*?)""\s*\/>", ResolveSeeHrefElement);
                    var summary = Regex.Replace(summaryXml, @"<(type)*paramref name=""([^\""]*)""\s*\/>", e => $"`{e.Groups[1].Value}`");
                    if (summary != "") summary = string.Join("  ", summary.Split(new[] { "\r", "\n", "\t" }, StringSplitOptions.RemoveEmptyEntries).Select(y => y.Trim()));

                    // Remarks body.
                    var remarksXml = x.Elements("remarks").FirstOrDefault()?.ToString()
                        ?? x.Element("remarks")?.ToString()
                        ?? "";
                    remarksXml = Regex.Replace(remarksXml, @"<\/?remarks>", string.Empty);
                    remarksXml = Regex.Replace(remarksXml, @"<para\s*/>", Environment.NewLine);
                    remarksXml = Regex.Replace(remarksXml, @"<see cref=""\w:([^\""]*)""\s*\/>", m => ResolveSeeElement(m, namespaceMatch));
                    remarksXml = Regex.Replace(remarksXml, @"<see href=""(.*?)""/>", ResolveSeeHrefElement);
                    var remarks = Regex.Replace(remarksXml, @"<(type)*paramref name=""([^\""]*)""\s*\/>", e => $"`{e.Groups[1].Value}`");
                    if (remarks != "") remarks = string.Join("  ", remarks.Split(new[] { "\r", "\n", "\t" }, StringSplitOptions.RemoveEmptyEntries).Select(y => y.Trim()));

                    var returns = ((string)x.Element("returns")) ?? "";
                    var example = ((string)x.Element("example")) ?? "";
                    if (example != "") example = string.Join(Environment.NewLine, // Trim each line of the example code.
                        example.Split(new[] { "\r", "\n", "\t" }, StringSplitOptions.None).Select(l => l.Trim()));
                    var parameters = x.Elements("param")
                        .Select(e => Tuple.Create(e.Attribute("name").Value, e))
                        .Distinct(new Item1EqualityCompaerer<string, XElement>())
                        .ToDictionary(e => e.Item1, e => e.Item2.Value);

                    var className = (memberType == MemberType.Type)
                        ? match.Groups[2].Value + "." + match.Groups[3].Value
                        : match.Groups[2].Value;

                    return new XmlDocumentComment {
                        MemberType = memberType,
                        ClassName = className,
                        MemberName = match.Groups[3].Value,
                        Summary = summary.Trim(),
                        Remarks = remarks.Trim(),
                        Example = example.Trim(),
                        Parameters = parameters,
                        Returns = returns.Trim()
                    };
                })
                .Where(x => x != null)
                .ToArray();
        }

        private static string ResolveSeeElement(Match m, string ns) {
            var typeName = m.Groups[1].Value;
            if (!string.IsNullOrWhiteSpace(ns)) {
                if (typeName.StartsWith(ns)) {
                    return $"[{typeName}]({Regex.Replace(typeName, $"\\.(?:.(?!\\.))+$", me => me.Groups[0].Value.Replace(".", "#").ToLower())})";
                }
            }
            return $"`{typeName}`";
        }

        private static string ResolveSeeHrefElement (Match m)
        {
            var href = m.Groups[1].Value;
            var name = href.GetAfterFirst("naninovel.com");
            return $"[{name}]({href})";
        }

        class Item1EqualityCompaerer<T1, T2> : EqualityComparer<Tuple<T1, T2>>
        {
            public override bool Equals(Tuple<T1, T2> x, Tuple<T1, T2> y)
            {
                return x.Item1.Equals(y.Item1);
            }

            public override int GetHashCode(Tuple<T1, T2> obj)
            {
                return obj.Item1.GetHashCode();
            }
        }
    }
}
