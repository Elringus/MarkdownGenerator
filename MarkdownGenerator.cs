using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MarkdownWikiGenerator
{
    public class MarkdownableType
    {
        public Type Type { get; private set; }
        public string Namespace => Type.Namespace;
        public string Name => Type.Name;
        public string BeautifyName => Beautifier.BeautifyType(Type);
        public bool IsCommand
        {
            get
            {
                    var curType = Type.BaseType;
                    while (curType != null)
                    {
                        if (curType.FullName == commandTypeFullName) return true;
                        curType = curType.BaseType;
                    }
                    return false;
            }
        }
        public bool HasCommandAlias => IsCommand && Type.CustomAttributes.Any(a => a.AttributeType.Name == commandAliasAttrName);
        public string CommandAlias => GetCommandAlias();

        private readonly ILookup<string, XmlDocumentComment> commentLookup;
        private const string commandTypeFullName = "Naninovel.Commands.Command";
        private const string commandAliasAttrName = "CommandAliasAttribute";
        private const string paramInterfaceName = "ICommandParameter";
        private const string paramAliasAttrName = "ParameterAliasAttribute";
        private const string requiredParamAttrName = "RequiredParameterAttribute";
        private const string resourcePathPrefixAttrName = "ResourcePathPrefixAttribute";
        private const string localizableInterfaceName = "ILocalizable";

        public MarkdownableType (Type type, ILookup<string, XmlDocumentComment> commentLookup)
        {
            Type = type;
            this.commentLookup = commentLookup;
        }

        public override string ToString () => IsCommand ? CommandToString() : GeneralToString();
        
        public JToken ToJson ()
        {
            dynamic commandJson = new JObject();

            commandJson.id = Type.Name;

            if (Type.CustomAttributes.Any(a => a.AttributeType.Name == commandAliasAttrName))
                commandJson.alias = Type.CustomAttributes.First(a => a.AttributeType.Name == commandAliasAttrName).ConstructorArguments.First().Value;

            commandJson.localizable = Type.GetInterfaces().Any(t => t.Name == localizableInterfaceName);

            var summary = commentLookup[Type.FullName].FirstOrDefault(x => x.MemberType == MemberType.Type)?.Summary;
            if (!string.IsNullOrWhiteSpace(summary))
                commandJson.summary = ReplaceLocalUrls(summary);

            var remarks = commentLookup[Type.FullName].FirstOrDefault(x => x.MemberType == MemberType.Type)?.Remarks;
            if (!string.IsNullOrWhiteSpace(remarks))
                commandJson.remarks = ReplaceLocalUrls(remarks);
            
            var examples = commentLookup[Type.FullName].FirstOrDefault(x => x.MemberType == MemberType.Type)?.Example;
            if (!string.IsNullOrWhiteSpace(examples))
                commandJson.examples = examples;

            var paramsJArray = new JArray();
            var commandParams = GetParameters();
            foreach (var parameter in commandParams)
            {
                // Extracting parameter properties.
                var id = parameter.Name;
                var alias = parameter.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == paramAliasAttrName)?.ConstructorArguments[0].Value as string;
                var nameless = alias == string.Empty;
                var required = parameter.CustomAttributes.Any(a => a.AttributeType.Name == requiredParamAttrName);
                var resourcePathPrefix = parameter.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == resourcePathPrefixAttrName)?.ConstructorArguments[0].Value as string;

                // Extracting parameter value type.
                string ResolveValueType (Type type)
                {
                    var valueTypeName = type.GetInterface("INullable`1")?.GetGenericArguments()[0]?.Name;
                    switch (valueTypeName)
                    {
                        case "String": case "NullableString": return "string";
                        case "Int32": case "NullableInteger": return "int";
                        case "Single": case "NullableFloat": return "float";
                        case "Boolean": case "NullableBoolean": return "bool";
                    }
                    return null;
                }
                dynamic paramDataType = new JObject();
                var paramType = parameter.FieldType;
                var isLiteral = ResolveValueType(paramType) != null;
                if (isLiteral)
                {
                    paramDataType.kind = "literal";
                    paramDataType.contentType = ResolveValueType(paramType);
                }
                else if (paramType.GetInterface("IEnumerable") != null)
                {
                    var elementType = paramType.GetInterface("INullable`1").GetGenericArguments()[0].GetGenericArguments()[0];
                    var namedElementType = elementType.BaseType?.GetGenericArguments()[0];
                    if (namedElementType?.GetInterface("INamedValue") != null) // Treating arrays of named liters as maps for the parser.
                    {
                        paramDataType.kind = "map";
                        paramDataType.contentType = ResolveValueType(namedElementType.GetInterface("INamed`1").GetGenericArguments()[0]);
                    }
                    else
                    {
                        paramDataType.kind = "array";
                        paramDataType.contentType = ResolveValueType(elementType);
                    }
                }
                else
                {
                    paramDataType.kind = "namedLiteral";
                    paramDataType.contentType = ResolveValueType(paramType.GetInterface("INullable`1").GetGenericArguments()[0].GetInterface("INamed`1").GetGenericArguments()[0]);
                }

                // Extracting parameter summary.
                var doc = default(XmlDocumentComment);
                var baseType = Type;
                while (baseType != null && doc is null)
                {
                    var key = baseType.FullName != null && baseType.FullName.Contains("[") ? baseType.FullName.GetBefore("[") : baseType.FullName;
                    doc = commentLookup[key].FirstOrDefault(x => x.MemberName == parameter.Name || x.MemberName.StartsWith(parameter.Name + "`"));
                    baseType = baseType.BaseType;
                }
                var paramSummary = doc?.Summary;

                // Building parameter json.
                dynamic paramJson = new JObject();
                paramJson.id = id;
                if (!string.IsNullOrWhiteSpace(alias))
                    paramJson.alias = alias;
                paramJson.nameless = nameless;
                paramJson.required = required;
                paramJson.dataType = paramDataType;
                if (!string.IsNullOrWhiteSpace(paramSummary))
                    paramJson.summary = ReplaceLocalUrls(paramSummary);
                if (!string.IsNullOrWhiteSpace(resourcePathPrefix))
                    paramJson.resourcePathPrefix = resourcePathPrefix;

                paramsJArray.Add(paramJson);
            }

            commandJson["params"] = paramsJArray;

            return commandJson;
        }

        private string ReplaceLocalUrls (string content)
        {
            content = Regex.Replace(content, @"\[@(\w+?)]", "[@$1](https://naninovel.com/api/#$1)");
            if (content.Contains("/api/#"))
                content = content.Replace(content.GetAfterFirst("/api/#"), content.GetAfterFirst("/api/#").ToLowerInvariant());
            return content.Replace("](/", "](https://naninovel.com/").Replace(".md", ".html");
        }

        private string GetCommandAlias ()
        {
            if (!HasCommandAlias) return null;
            var aliasAttr = Type.CustomAttributes.First(a => a.AttributeType.Name == commandAliasAttrName);
            return aliasAttr.ConstructorArguments.First().Value as string;
        }

        private MethodInfo[] GetMethods ()
        {
            return Type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.InvokeMethod)
                .Where(x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any() && !x.IsPrivate)
                .ToArray();
        }

        private PropertyInfo[] GetProperties ()
        {
            return Type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.GetProperty | BindingFlags.SetProperty)
                .Where(x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any())
                .Where(y => {
                    var get = y.GetGetMethod(true);
                    var set = y.GetSetMethod(true);
                    if (get != null && set != null) return !(get.IsPrivate && set.IsPrivate);
                    else if (get != null) return !get.IsPrivate;
                    else if (set != null) return !set.IsPrivate;
                    else return false;
                }).ToArray();
        }

        private FieldInfo[] GetFields ()
        {
            return Type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.GetField | BindingFlags.SetField)
                .Where(x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any() && !x.IsPrivate).ToArray();
        }

        private FieldInfo[] GetParameters ()
        {
            return Type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any())
                .Where(f => f.FieldType.GetInterface(paramInterfaceName) != null).ToArray();
        }

        private EventInfo[] GetEvents ()
        {
            return Type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any()).ToArray();
        }

        private FieldInfo[] GetStaticFields ()
        {
            return Type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.GetField | BindingFlags.SetField)
                .Where(x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any() && !x.IsPrivate).ToArray();
        }

        private PropertyInfo[] GetStaticProperties ()
        {
            return Type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.GetProperty | BindingFlags.SetProperty)
                .Where(x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any())
                .Where(y => {
                    var get = y.GetGetMethod(true);
                    var set = y.GetSetMethod(true);
                    if (get != null && set != null) return !(get.IsPrivate && set.IsPrivate);
                    else if (get != null) return !get.IsPrivate;
                    else if (set != null) return !set.IsPrivate;
                    else return false;
                }).ToArray();
        }

        private MethodInfo[] GetStaticMethods ()
        {
            return Type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.InvokeMethod)
                .Where(x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any() && !x.IsPrivate).ToArray();
        }

        private EventInfo[] GetStaticEvents ()
        {
            return Type.GetEvents(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(x => !x.IsSpecialName && !x.GetCustomAttributes<ObsoleteAttribute>().Any()).ToArray();
        }

        private void BuildTable<T> (MarkdownBuilder mb, string label, T[] array, IEnumerable<XmlDocumentComment> docs, Func<T, string> type, Func<T, string> name, Func<T, string> finalName)
        {
            if (array.Any())
            {
                mb.Header(4, label);
                mb.AppendLine();

                var head = Type.IsEnum ? new[] { "Value", "Name", "Summary" } : new[] { "Type", "Name", "Summary" };

                IEnumerable<T> seq = array;
                if (!Type.IsEnum) seq = array.OrderBy(x => name(x));

                var data = seq.Select(item2 => {
                    var summary = docs.FirstOrDefault(x => x.MemberName == name(item2) || x.MemberName.StartsWith(name(item2) + "`"))?.Summary ?? "";
                    return new[] { type(item2), finalName(item2), summary };
                });

                mb.Table(head, data);
                mb.AppendLine();
            }
        }

        private string CommandToString ()
        {
            var mb = new MarkdownBuilder();

            if (HasCommandAlias) mb.Header(2, CommandAlias);
            else
            {
                var typeName = Beautifier.BeautifyType(Type, false);
                typeName = char.ToLowerInvariant(typeName[0]) + (typeName.Length > 1 ? typeName.Substring(1) : string.Empty);
                mb.Header(2, typeName);
            }
            mb.AppendLine();

            var summary = commentLookup[Type.FullName].FirstOrDefault(x => x.MemberType == MemberType.Type)?.Summary;
            if (!string.IsNullOrWhiteSpace(summary))
            {
                mb.Header(4, "Summary");
                mb.AppendLine(summary);
                mb.AppendLine();
            }

            var remarks = commentLookup[Type.FullName].FirstOrDefault(x => x.MemberType == MemberType.Type)?.Remarks;
            if (!string.IsNullOrWhiteSpace(remarks))
            {
                mb.Header(4, "Remarks");
                mb.AppendLine(remarks);
                mb.AppendLine();
            }

            // Params ----------------------------------------
            var parameters = GetParameters().Where(f => f.Name != "Wait" && f.Name != "ConditionalExpression");

            if (parameters.Count() > 0)
            {
                mb.Header(4, "Parameters");
                mb.Append("\n<div class=\"config-table\">\n\n");
                mb.Append("ID | Type | Description\n");
                mb.Append("--- | --- | ---\n");
                foreach (var parameter in parameters)
                {
                    var paramAlias = parameter.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == paramAliasAttrName)?.ConstructorArguments[0].Value as string;
                    var paramId = paramAlias ?? parameter.Name;
                    var isNameless = paramId == string.Empty;
                    var isOptional = !parameter.CustomAttributes.Any(a => a.AttributeType.Name == requiredParamAttrName);
                    if (isNameless) paramId = parameter.Name;
                    else paramId = char.ToLowerInvariant(paramId[0]) + (paramId.Length > 1 ? paramId.Substring(1) : string.Empty);
                    if (isNameless || !isOptional)
                    {
                        var style = (isNameless ? "command-param-nameless " : string.Empty) + (!isOptional ? "command-param-required" : string.Empty);
                        style = style.Trim();
                        var title = (isNameless ? "Nameless parameter: value should be provided after the command identifer without specifying parameter ID " : string.Empty) +
                            (!isOptional ? " Required parameter: parameter should always be specified" : string.Empty);
                        title = title.Trim();
                        paramId = $"<span class=\"{style}\" title=\"{title}\">{paramId}</span>";
                    }

                    var typeName = Beautifier.BeautifyType(parameter.FieldType);

                    var doc = default(XmlDocumentComment);
                    var baseType = Type;
                    while (baseType != null && doc is null)
                    {
                        var key = baseType.FullName != null && baseType.FullName.Contains("[") ? baseType.FullName.GetBefore("[") : baseType.FullName;
                        doc = commentLookup[key].FirstOrDefault(x => x.MemberName == parameter.Name || x.MemberName.StartsWith(parameter.Name + "`"));
                        baseType = baseType.BaseType;
                    }
                    var descr = doc?.Summary ?? string.Empty;
                    mb.Append($"{paramId} | {typeName} | {descr}\n");
                }
                mb.Append("\n</div>\n\n");
            }
            // -----------------------------------------------

            var example = commentLookup[Type.FullName].FirstOrDefault(x => x.MemberType == MemberType.Type)?.Example;
            if (!string.IsNullOrWhiteSpace(example))
            {
                mb.Header(4, "Example");
                mb.Code(example);
                mb.AppendLine();
            }

            return mb.ToString();
        }

        private string GeneralToString ()
        {
            var mb = new MarkdownBuilder();

            mb.Header(2, Beautifier.BeautifyType(Type, false));
            mb.AppendLine();

            var summary = commentLookup[Type.FullName].FirstOrDefault(x => x.MemberType == MemberType.Type)?.Summary;
            if (!string.IsNullOrWhiteSpace(summary))
            {
                mb.Header(4, "Summary");
                mb.AppendLine(summary);
            }

            var remarks = commentLookup[Type.FullName].FirstOrDefault(x => x.MemberType == MemberType.Type)?.Remarks;
            if (!string.IsNullOrWhiteSpace(remarks))
            {
                mb.Header(4, "Remarks");
                mb.AppendLine(remarks);
            }

            { // Signature code.
                var stat = (Type.IsAbstract && Type.IsSealed) ? "static " : "";
                var abst = (Type.IsAbstract && !Type.IsInterface && !Type.IsSealed) ? "abstract " : "";
                var classOrStructOrEnumOrInterface = Type.IsInterface ? "interface" : Type.IsEnum ? "enum" : Type.IsValueType ? "struct" : "class";

                var sb = new StringBuilder();
                sb.AppendLine($"public {stat}{abst}{classOrStructOrEnumOrInterface} {Beautifier.BeautifyType(Type, true)}");
                var impl = string.Join(", ", new[] { Type.BaseType }.Concat(Type.GetInterfaces()).Where(x => x != null && x != typeof(object) && x != typeof(ValueType)).Select(x => Beautifier.BeautifyType(x)));
                if (impl != "") sb.AppendLine("    : " + impl);

                mb.Code("csharp", sb.ToString());
                mb.AppendLine();
            }

            if (Type.IsEnum)
            {
                var enums = Enum.GetNames(Type)
                    .Select(x => new { Name = x, Value = (int)Enum.Parse(Type, x) })
                    .OrderBy(x => x.Value).ToArray();

                BuildTable(mb, "Enum", enums, commentLookup[Type.FullName], x => x.Value.ToString(), x => x.Name, x => x.Name);
            }
            else
            {
                BuildTable(mb, "Fields", GetFields(), commentLookup[Type.FullName], x => Beautifier.BeautifyType(x.FieldType), x => x.Name, x => x.Name);
                BuildTable(mb, "Properties", GetProperties(), commentLookup[Type.FullName], x => Beautifier.BeautifyType(x.PropertyType), x => x.Name, x => x.Name);
                BuildTable(mb, "Events", GetEvents(), commentLookup[Type.FullName], x => Beautifier.BeautifyType(x.EventHandlerType), x => x.Name, x => x.Name);
                BuildTable(mb, "Methods", GetMethods(), commentLookup[Type.FullName], x => Beautifier.BeautifyType(x.ReturnType), x => x.Name, x => Beautifier.ToMarkdownMethodInfo(x));
                BuildTable(mb, "Static Fields", GetStaticFields(), commentLookup[Type.FullName], x => Beautifier.BeautifyType(x.FieldType), x => x.Name, x => x.Name);
                BuildTable(mb, "Static Properties", GetStaticProperties(), commentLookup[Type.FullName], x => Beautifier.BeautifyType(x.PropertyType), x => x.Name, x => x.Name);
                BuildTable(mb, "Static Methods", GetStaticMethods(), commentLookup[Type.FullName], x => Beautifier.BeautifyType(x.ReturnType), x => x.Name, x => Beautifier.ToMarkdownMethodInfo(x));
                BuildTable(mb, "Static Events", GetStaticEvents(), commentLookup[Type.FullName], x => Beautifier.BeautifyType(x.EventHandlerType), x => x.Name, x => x.Name);
            }

            return mb.ToString();
        }
    }

    public static class MarkdownGenerator
    {
        public static MarkdownableType[] Load (string dllPath, string namespaceMatch)
        {
            var xmlPath = Path.Combine(Directory.GetParent(dllPath).FullName, Path.GetFileNameWithoutExtension(dllPath) + ".xml");

            var comments = new XmlDocumentComment[0];
            if (File.Exists(xmlPath))
            {
                var fileText = File.ReadAllText(xmlPath, Encoding.UTF8);
                comments = VSDocParser.ParseXmlComment(XDocument.Parse(fileText), namespaceMatch);
            }
            var commentsLookup = comments.ToLookup(x => x.ClassName);

            var namespaceRegex = !string.IsNullOrEmpty(namespaceMatch) ? new Regex(namespaceMatch) : null;

            var markdownableTypes = new[] { Assembly.LoadFrom(dllPath) }
                .SelectMany(x => {
                    try
                    {
                        return x.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        return ex.Types.Where(t => t != null);
                    }
                    catch
                    {
                        return Type.EmptyTypes;
                    }
                })
                .Where(x => x != null)
                .Where(x => x.IsPublic && !typeof(Delegate).IsAssignableFrom(x) && !x.GetCustomAttributes<ObsoleteAttribute>().Any())
                .Where(x => IsRequiredNamespace(x, namespaceRegex))
                .Select(x => new MarkdownableType(x, commentsLookup))
                .ToArray();

            return markdownableTypes;
        }

        static bool IsRequiredNamespace (Type type, Regex regex)
        {
            if (regex == null) return true;
            return regex.IsMatch(type.Namespace ?? string.Empty);
        }
    }
}
