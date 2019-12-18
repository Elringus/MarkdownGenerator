using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MarkdownWikiGenerator
{
    public static class Beautifier
    {
        public static string BeautifyType(Type t, bool isFull = false)
        {
            if (t == null) return "";
            if (t == typeof(void)) return "void";

            if (t.GetInterface("ICommandParameter") != null)
            {
                switch (t.Name)
                {
                    case "StringParameter": return "String";
                    case "IntegerParameter": return "Integer";
                    case "DecimalParameter": return "Decimal";
                    case "BooleanParameter": return "Boolean";
                    case "NamedStringParameter": return "Named&lt;String&gt;";
                    case "NamedIntegerParameter": return "Named&lt;Integer&gt;";
                    case "NamedDecimalParameter": return "Named&lt;Decimal&gt;";
                    case "NamedBooleanParameter": return "Named&lt;Boolean&gt;";
                    case "StringListParameter": return "List&lt;String&gt;";
                    case "IntegerListParameter": return "List&lt;Integer&gt;";
                    case "DecimalListParameter": return "List&lt;Decimal&gt;";
                    case "BooleanListParameter": return "List&lt;Boolean&gt;";
                    case "NamedStringListParameter": return "List&lt;Named&lt;String&gt;&gt;";
                    case "NamedIntegerListParameter": return "List&lt;Named&lt;Integer&gt;&gt;";
                    case "NamedDecimalListParameter": return "List&lt;Named&lt;Decimal&gt;&gt;";
                    case "NamedBooleanListParameter": return "List&lt;Named&lt;Boolean&gt;&gt;";
                }

                return "Unknown";
            }

            if (Nullable.GetUnderlyingType(t) != null) t = Nullable.GetUnderlyingType(t);
            if (t.IsArray) return $"List&lt;{BeautifyType(t.GetElementType())}&gt;";
            if (!t.IsGenericType) return (isFull ? t.FullName : t.Name).Replace("Int32", "Integer").Replace("Single", "Decimal"); ;

            var innerFormat = string.Join(", ", t.GetGenericArguments().Select(x => BeautifyType(x)));
            return Regex.Replace(isFull ? t.GetGenericTypeDefinition().FullName : t.GetGenericTypeDefinition().Name, @"`.+$", "") + "&lt;" + innerFormat + "&gt;";
        }

        public static string ToMarkdownMethodInfo(MethodInfo methodInfo)
        {
            var isExtension = methodInfo.GetCustomAttributes<System.Runtime.CompilerServices.ExtensionAttribute>(false).Any();

            var seq = methodInfo.GetParameters().Select(x =>
            {
                var suffix = x.HasDefaultValue ? (" = " + (x.DefaultValue ?? $"null")) : "";
                return "`" + BeautifyType(x.ParameterType) + "` " + x.Name + suffix;
            });

            return methodInfo.Name.Replace("<", "&lt;").Replace(">", "&gt;") + "(" + (isExtension ? "this " : "") + string.Join(", ", seq) + ")";
        }
    }
}
